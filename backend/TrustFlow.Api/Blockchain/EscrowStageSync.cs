using System.Data;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Blockchain;

public sealed class EscrowStageSync(AppDbContext db, IEscrowChainDeployer chain)
{
    public async Task<bool> ReconcileAsync(Guid escrowId, OnChainEscrowSnapshot snapshot, int decimals, CancellationToken ct)
    {
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(e => e.Id == escrowId, ct);
        var stages = await db.EscrowFundingStages.AsNoTracking().Where(s => s.EscrowId == escrowId).ToListAsync(ct);
        var events = await chain.GetStageEventsAsync(escrow.ContractAddress!, escrow.DeploymentTransactionHash!, ct);
        decimal deposited = 0;
        foreach (var evidence in events)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(evidence.TransactionHash, "^0x[0-9a-fA-F]{64}$") ||
                !System.Text.RegularExpressions.Regex.IsMatch(evidence.BlockHash, "^0x[0-9a-fA-F]{64}$") ||
                evidence.BlockNumber < 0 || evidence.LogIndex < 0 || evidence.OccurredAt <= DateTimeOffset.UnixEpoch)
                throw new InvalidOperationException("Stage evidence is incomplete.");
            var stage = stages.SingleOrDefault(s => s.ChainIndex == evidence.Index)
                ?? throw new InvalidOperationException("The chain contains unreserved scope. Reconciliation is blocked.");
            var snapshotHash = "0x" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(stage.TermsJson))).ToLowerInvariant();
            if (!Same(snapshotHash, stage.TermsHash))
                throw new InvalidOperationException("The saved terms snapshot does not match its funded commitment.");
            if (!Same(stage.FundingReference, evidence.FundingReference) || !Same(stage.TermsHash, evidence.TermsHash) ||
                !Same(evidence.Sender, evidence.Kind == "Funding" ? escrow.ClientWalletAddress : escrow.FreelancerWalletAddress))
                throw new InvalidOperationException("Stage reference, terms or signer differ from the reserved scope.");
            if (evidence.Kind == "Funding")
            {
                if (evidence.Amount != TokenAmountConverter.ToBaseUnits(stage.Amount, decimals))
                    throw new InvalidOperationException("The deposit does not match the full milestone amount.");
                deposited += stage.Amount;
            }
            else if (evidence.Kind != "Start" || !events.Any(e => e.Kind == "Funding" && e.Index == evidence.Index &&
                         (e.BlockNumber < evidence.BlockNumber || e.BlockNumber == evidence.BlockNumber && e.LogIndex < evidence.LogIndex)))
                throw new InvalidOperationException("A work start has no preceding funded scope.");
        }
        if (events.Select(e => (e.Kind, e.Index)).Distinct().Count() != events.Count ||
            snapshot.TotalAmount != TokenAmountConverter.ToBaseUnits(deposited, decimals) ||
            (snapshot.State == OnChainEscrowState.Funded && !events.Any(e => e.Kind == "Funding" && e.Index == snapshot.NextMilestoneIndex)) ||
            (snapshot.WorkStarted && !events.Any(e => e.Kind == "Start" && e.Index == snapshot.NextMilestoneIndex) &&
             snapshot.State == OnChainEscrowState.Funded))
            throw new InvalidOperationException("The contract deposit/start state has incomplete stage evidence.");

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var changed = 0;
        foreach (var evidence in events)
        {
            var stage = stages.Single(s => s.ChainIndex == evidence.Index);
            if (evidence.Kind == "Funding")
            {
                if (stage.FundingTransactionHash != null && !Same(stage.FundingTransactionHash, evidence.TransactionHash))
                    throw new InvalidOperationException("Stored funding evidence conflicts with the chain.");
                changed += await db.EscrowFundingStages.Where(s => s.Id == stage.Id && s.FundingTransactionHash == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.FundingTransactionHash, evidence.TransactionHash)
                        .SetProperty(s => s.FundedAt, evidence.OccurredAt), ct);
                if (stage.ProposalId.HasValue)
                    await db.Proposals.Where(p => p.Id == stage.ProposalId && p.Status == ProposalStatus.AwaitingFunding)
                        .ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, ProposalStatus.Funded), ct);

                // Selection reserves scope; only a verified deposit closes
                // competing bids for this particular milestone. Keep bundled
                // proposals intact and reject the whole overlapping bid.
                // Check current ownership/reservation in SQL so replaying an
                // old receipt cannot reject applicants after the scope exits.
                if (snapshot.State is OnChainEscrowState.Funded or OnChainEscrowState.Completed)
                {
                    var selectedProposalId = stage.ProposalId;
                    changed += await db.Proposals.Where(p => p.ProjectId == escrow.ProjectId &&
                            p.Status == ProposalStatus.Pending && (Guid?)p.Id != selectedProposalId &&
                            p.ProposalMilestones.Any(link => link.MilestoneId == stage.MilestoneId) &&
                            db.EscrowFundingStages.Any(current => current.Id == stage.Id && current.ReservationActive &&
                                current.FundingTransactionHash == evidence.TransactionHash &&
                                current.Escrow.ProjectId == escrow.ProjectId &&
                                current.Escrow.Status != EscrowStatus.Cancelled && current.Escrow.Status != EscrowStatus.Terminated &&
                                current.Milestone.ProjectId == escrow.ProjectId &&
                                current.Milestone.FreelancerId == escrow.FreelancerId && current.Milestone.OnChainIndex == evidence.Index))
                        .ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, ProposalStatus.Rejected), ct);
                }
            }
            else
            {
                if (stage.StartTransactionHash != null && !Same(stage.StartTransactionHash, evidence.TransactionHash))
                    throw new InvalidOperationException("Stored start evidence conflicts with the chain.");
                changed += await db.EscrowFundingStages.Where(s => s.Id == stage.Id && s.StartTransactionHash == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.StartTransactionHash, evidence.TransactionHash)
                        .SetProperty(s => s.StartedAt, evidence.OccurredAt), ct);
                if (snapshot.State == OnChainEscrowState.Funded && evidence.Index == snapshot.NextMilestoneIndex && stage.ReservationActive)
                {
                    await db.Milestones.Where(m => m.Id == stage.MilestoneId && m.Status == MileStoneStatus.Pending && m.FreelancerId == escrow.FreelancerId)
                        .ExecuteUpdateAsync(set => set.SetProperty(m => m.Status, MileStoneStatus.InProgress), ct);
                    await db.Projects.Where(p => p.Id == escrow.ProjectId && (p.Status == ProjectStatus.Open || p.Status == ProjectStatus.Completed))
                        .ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, ProjectStatus.InProgress), ct);
                }
                if (stage.ProposalId.HasValue && stage.ReservationActive)
                    await db.Proposals.Where(p => p.Id == stage.ProposalId &&
                            (p.Status == ProposalStatus.Funded || p.Status == ProposalStatus.AwaitingFunding))
                        .ExecuteUpdateAsync(set => set.SetProperty(p => p.Status, ProposalStatus.Accepted), ct);
            }
        }
        if (deposited > 0)
        {
            var first = events.Where(e => e.Kind == "Funding").OrderBy(e => e.Index).First();
            await db.Escrows.Where(e => e.Id == escrowId).ExecuteUpdateAsync(set => set
                .SetProperty(e => e.TotalAmount, deposited)
                .SetProperty(e => e.FundingTransactionHash, first.TransactionHash)
                .SetProperty(e => e.FundedAt, first.OccurredAt), ct);
            // Reopen only after a new verified deposit, never on a prepared plan entry.
            if (snapshot.State == OnChainEscrowState.Funded)
                await db.Escrows.Where(e => e.Id == escrowId && (e.Status == EscrowStatus.AwaitingFunding || e.Status == EscrowStatus.Completed))
                    .ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, EscrowStatus.Funded)
                        .SetProperty(e => e.CompletedAt, (DateTimeOffset?)null), ct);
        }
        if (snapshot.State is OnChainEscrowState.Cancelled or OnChainEscrowState.Terminated)
            await db.EscrowFundingStages.Where(s => s.EscrowId == escrowId && s.ReservationActive)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.ReservationActive, false), ct);
        await tx.CommitAsync(ct);
        return changed > 0;
    }

    private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
