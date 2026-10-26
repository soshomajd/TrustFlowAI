using System.Numerics;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;

namespace TrustFlow.Api.Blockchain;

public interface IEscrowExecutionTrace
{
    Task<bool> CaptureAsync(Guid escrowId, CancellationToken cancellationToken);
}

public sealed class EscrowExecutionTrace(AppDbContext db, IEscrowChainDeployer chain) : IEscrowExecutionTrace
{
    public async Task<bool> CaptureAsync(Guid escrowId, CancellationToken cancellationToken)
    {
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrowId, cancellationToken);
        if (string.IsNullOrWhiteSpace(escrow.ContractAddress) || string.IsNullOrWhiteSpace(escrow.DeploymentTransactionHash))
            throw new InvalidOperationException("Execution history requires the confirmed deployment.");
        if (await chain.GetChainIdAsync(cancellationToken) != escrow.ChainId)
            throw new InvalidOperationException("Execution history RPC belongs to another chain.");
        var snapshot = await chain.GetEscrowSnapshotAsync(escrow.ContractAddress, cancellationToken);
        var decimals = await chain.GetTokenDecimalsAsync(escrow.TokenAddress, cancellationToken);
        if (!Same(snapshot.Token, escrow.TokenAddress) || !Same(snapshot.Client, escrow.ClientWalletAddress) ||
            !Same(snapshot.Freelancer, escrow.FreelancerWalletAddress) || (escrow.ArbiterWalletAddress is not null && !Same(snapshot.Arbiter, escrow.ArbiterWalletAddress)) ||
            snapshot.TotalAmount != TokenAmountConverter.ToBaseUnits(escrow.TotalAmount, decimals))
            throw new InvalidOperationException("Execution history does not match the stored contract participants and total.");
        if (escrow.PlatformFeeBasisPoints > 0)
        {
            var policy = await chain.GetFeePolicyAsync(escrow.ContractAddress, cancellationToken);
            if (policy.BasisPoints != escrow.PlatformFeeBasisPoints || !Same(policy.Recipient, escrow.FeeRecipientAddress))
                throw new InvalidOperationException("Execution history has a different fee policy.");
        }
        var milestones = await db.Milestones.AsNoTracking().Where(item => item.ProjectId == escrow.ProjectId && item.FreelancerId == escrow.FreelancerId)
            .OrderBy(item => item.SequenceNumber).ToListAsync(cancellationToken);
        if (snapshot.NextMilestoneIndex < 0 || snapshot.NextMilestoneIndex > milestones.Count)
            throw new InvalidOperationException("Execution history has an invalid milestone position.");
        var stored = await db.EscrowExecutionRecords.AsNoTracking().Where(item => item.EscrowId == escrowId).ToListAsync(cancellationToken);
        if ((!snapshot.WorkStarted || stored.Any(item => item.MilestoneIndex == -1)) &&
            Enumerable.Range(0, snapshot.NextMilestoneIndex).All(index => stored.Any(item => item.MilestoneIndex == index))) return false;

        var events = await chain.GetExecutionEventsAsync(escrow.ContractAddress, escrow.DeploymentTransactionHash, cancellationToken);
        if (events.Select(item => item.MilestoneIndex).Distinct().Count() != events.Count ||
            (snapshot.WorkStarted && events.All(item => item.Kind != "WorkStarted")) ||
            Enumerable.Range(0, snapshot.NextMilestoneIndex).Any(index => events.All(item => item.MilestoneIndex != index)))
            throw new InvalidOperationException("Execution events are missing or duplicated.");
        var pending = new List<EscrowExecutionRecord>();
        foreach (var item in events)
        {
            if (!Hex(item.TransactionHash, 64) || !Hex(item.BlockHash, 64) || !Hex(item.TransactionSender, 40) ||
                item.BlockNumber < 0 || item.LogIndex < 0 || item.OccurredAt <= DateTimeOffset.UnixEpoch)
                throw new InvalidOperationException("Execution evidence is incomplete.");
            Guid? milestoneId = null;
            if (item.Kind == "WorkStarted")
            {
                if (item.MilestoneIndex != -1 || !snapshot.WorkStarted || !Same(item.Participant, escrow.FreelancerWalletAddress))
                    throw new InvalidOperationException("Work-start event does not match this escrow.");
            }
            else
            {
                if (item.MilestoneIndex < 0 || item.MilestoneIndex >= snapshot.NextMilestoneIndex)
                    throw new InvalidOperationException("Payment event is outside the released stages.");
                var milestone = milestones[item.MilestoneIndex]; milestoneId = milestone.Id;
                var onChain = await chain.GetMilestoneAsync(escrow.ContractAddress, item.MilestoneIndex, cancellationToken, escrow.PlatformFeeBasisPoints > 0);
                EscrowFeePolicy.ValidatePayment(onChain, escrow.PlatformFeeBasisPoints);
                // An ending settlement returns this stage's remainder AND all later
                // stages. Earlier stages have already consumed their whole amounts,
                // regardless of how their individual awards/refunds were divided.
                var expectedRefund = onChain.Amount - item.GrossPaid;
                if (item.EscrowEnded)
                {
                    if (item.Kind != "CaseSettlement" ||
                        snapshot.State is not (OnChainEscrowState.Terminated or OnChainEscrowState.Completed) ||
                        item.MilestoneIndex != snapshot.NextMilestoneIndex - 1)
                        throw new InvalidOperationException("Ending settlement does not match the terminal escrow position.");
                    var priorStageAmounts = milestones.Take(item.MilestoneIndex).Aggregate(BigInteger.Zero,
                        (sum, stage) => sum + TokenAmountConverter.ToBaseUnits(stage.Amount, decimals));
                    expectedRefund = snapshot.TotalAmount - priorStageAmounts - item.GrossPaid;
                    var priorCaseRefunds = events.Where(previous => previous.MilestoneIndex >= 0 &&
                        previous.MilestoneIndex < item.MilestoneIndex).Aggregate(BigInteger.Zero,
                        (sum, previous) => sum + previous.Refunded);
                    if (expectedRefund < onChain.Amount - item.GrossPaid ||
                        snapshot.RefundedAmount != priorCaseRefunds + expectedRefund ||
                        snapshot.ReleasedAmount + snapshot.RefundedAmount != snapshot.TotalAmount)
                        throw new InvalidOperationException("Ending settlement does not conserve the escrow's exact remaining funds.");
                }
                if (!onChain.Released || onChain.Amount != TokenAmountConverter.ToBaseUnits(milestone.Amount, decimals) || item.GrossPaid != onChain.Paid ||
                    (item.Kind == "ClientRelease" && (onChain.SettledByCase || item.GrossPaid != onChain.Amount || item.Refunded != BigInteger.Zero || !Same(item.Participant, escrow.FreelancerWalletAddress))) ||
                    (item.Kind == "CaseSettlement" && (!onChain.SettledByCase || item.Refunded != expectedRefund)) ||
                    item.Kind is not ("ClientRelease" or "CaseSettlement"))
                    throw new InvalidOperationException("Payment event does not match the exact milestone outcome.");
            }
            var existing = stored.SingleOrDefault(row => row.MilestoneIndex == item.MilestoneIndex);
            if (existing is not null)
            {
                if (existing.Kind != item.Kind || !Same(existing.TransactionHash, item.TransactionHash) ||
                    !Same(existing.BlockHash, item.BlockHash) || existing.OccurredAt != item.OccurredAt || existing.MilestoneId != milestoneId)
                    throw new InvalidOperationException("Recorded execution evidence conflicts with the chain.");
                continue;
            }
            pending.Add(new EscrowExecutionRecord { EscrowId = escrowId, MilestoneId = milestoneId, MilestoneIndex = item.MilestoneIndex,
                Kind = item.Kind, TransactionHash = item.TransactionHash, BlockHash = item.BlockHash, BlockNumber = item.BlockNumber,
                LogIndex = item.LogIndex, OccurredAt = item.OccurredAt.ToUniversalTime(), TransactionSender = item.TransactionSender });
        }
        var changed = 0;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in pending)
        {
            // Concurrent sync/approval may discover the same evidence. Never increment accounting here.
            changed += await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "EscrowExecutionRecords" ("Id","EscrowId","MilestoneId","MilestoneIndex","Kind","TransactionHash","BlockHash","BlockNumber","LogIndex","OccurredAt","TransactionSender")
                VALUES ({row.Id},{row.EscrowId},{row.MilestoneId},{row.MilestoneIndex},{row.Kind},{row.TransactionHash},{row.BlockHash},{row.BlockNumber},{row.LogIndex},{row.OccurredAt},{row.TransactionSender})
                ON CONFLICT ("EscrowId","MilestoneIndex") DO NOTHING
                """, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return changed > 0;
    }

    private static bool Same(string? first, string? second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    private static bool Hex(string? text, int digits) => text is not null && Regex.IsMatch(text, $"^0x[0-9a-fA-F]{{{digits}}}$");
}
