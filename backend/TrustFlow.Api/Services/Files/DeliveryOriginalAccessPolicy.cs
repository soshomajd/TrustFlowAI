using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Files;

// An approval button, a deposit, or a partial settlement does not buy the
// original. Entitlement comes from the reconciled full gross award and its
// canonical execution receipt for this exact freelancer and milestone.
public sealed class DeliveryOriginalAccessPolicy(AppDbContext db)
{
    public async Task<bool> ClientHasOriginalsAsync(Guid projectId, Guid milestoneId,
        Guid freelancerId, CancellationToken cancellationToken)
    {
        var milestone = await db.Milestones.AsNoTracking()
            .Where(item => item.Id == milestoneId && item.ProjectId == projectId &&
                item.FreelancerId == freelancerId)
            .Select(item => new { item.Amount, item.PaidAmount, item.Status, item.OnChainIndex, item.SequenceNumber })
            .SingleOrDefaultAsync(cancellationToken);
        if (milestone is null || milestone.Amount <= 0 || milestone.PaidAmount != milestone.Amount ||
            milestone.Status is not (MileStoneStatus.Approved or MileStoneStatus.Settled)) return false;

        var escrow = await db.Escrows.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectId == projectId && item.FreelancerId == freelancerId,
                cancellationToken);
        if (escrow is null || escrow.ChainId <= 0 || !Hex(escrow.ContractAddress, 40) ||
            !Hex(escrow.ClientWalletAddress, 40) || !Hex(escrow.FreelancerWalletAddress, 40)) return false;

        int? index;
        if (escrow.ProtocolVersion == 3)
        {
            index = await db.EscrowFundingStages.AsNoTracking()
                .Where(item => item.EscrowId == escrow.Id && item.MilestoneId == milestoneId && item.FundedAt != null)
                .Select(item => item.ChainIndex).SingleOrDefaultAsync(cancellationToken);
            if (index is null || (milestone.OnChainIndex is { } mapped && mapped != index)) return false;
        }
        else
        {
            index = await db.Milestones.AsNoTracking().CountAsync(item => item.ProjectId == projectId &&
                item.FreelancerId == freelancerId && item.SequenceNumber < milestone.SequenceNumber, cancellationToken);
            if (milestone.OnChainIndex is { } mapped && mapped != index) return false;
        }

        var kind = milestone.Status == MileStoneStatus.Approved ? "ClientRelease" : "CaseSettlement";
        var receipt = await db.EscrowExecutionRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.EscrowId == escrow.Id && item.MilestoneId == milestoneId &&
                item.MilestoneIndex == index && item.Kind == kind, cancellationToken);
        if (receipt is null || !Hex(receipt.TransactionHash, 64) || !Hex(receipt.BlockHash, 64) ||
            receipt.BlockNumber < 0 || receipt.LogIndex < 0 || receipt.OccurredAt <= DateTimeOffset.UnixEpoch ||
            !Hex(receipt.TransactionSender, 40)) return false;

        if (kind == "ClientRelease") return Same(receipt.TransactionSender, escrow.ClientWalletAddress);

        // A full award may be signed by the fixed arbiter. The contract's
        // participant fallback is also a canonical case-settlement sender;
        // its usual 50/50 amount still fails the full-gross check above.
        return (Same(receipt.TransactionSender, escrow.ArbiterWalletAddress) ||
                Same(receipt.TransactionSender, escrow.ClientWalletAddress) ||
                Same(receipt.TransactionSender, escrow.FreelancerWalletAddress)) &&
            await db.EscrowCases.AsNoTracking().AnyAsync(item => item.EscrowId == escrow.Id &&
                item.MilestoneId == milestoneId && item.Status == EscrowCaseStatus.Settled &&
                item.PaidToFreelancer == milestone.Amount, cancellationToken);
    }

    private static bool Same(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool Hex(string? value, int digits) => value is not null && value.Length == digits + 2 &&
        value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && value.AsSpan(2).ContainsAnyExcept("0123456789abcdefABCDEF") == false;
}
