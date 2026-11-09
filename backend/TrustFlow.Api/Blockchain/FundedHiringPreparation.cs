using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Blockchain;

public sealed class FundedHiringPreparation(AppDbContext db, IOptions<BlockchainOptions> options)
{
    public async Task<Guid> PrepareAsync(Guid projectId, Guid proposalId, Guid clientId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var project = await db.Projects.SingleOrDefaultAsync(p => p.Id == projectId && p.ClientId == clientId, ct)
            ?? throw new KeyNotFoundException("Project not found.");
        var proposal = await db.Proposals.Include(p => p.ProposalMilestones).ThenInclude(p => p.Milestone)
            .SingleOrDefaultAsync(p => p.Id == proposalId && p.ProjectId == projectId, ct)
            ?? throw new KeyNotFoundException("Proposal not found.");
        var resumed = await db.EscrowFundingStages.Where(s => s.ProposalId == proposalId)
            .Select(s => (Guid?)s.EscrowId).FirstOrDefaultAsync(ct);
        if (resumed.HasValue) return resumed.Value;
        if (proposal.Status != ProposalStatus.Pending || project.Status is not (ProjectStatus.Open or ProjectStatus.InProgress))
            throw new InvalidOperationException("Only an available pending proposal can be selected.");
        var stages = proposal.ProposalMilestones.Select(s => s.Milestone).OrderBy(s => s.SequenceNumber).ToList();
        if (stages.Count == 0 || stages.Sum(s => s.Amount) != proposal.BidAmount ||
            await db.Milestones.Where(s => s.ProjectId == projectId).SumAsync(s => s.Amount, ct) != project.Budget)
            throw new InvalidOperationException("The proposal no longer matches the milestone plan. Review its prices first.");
        var ids = stages.Select(s => s.Id).ToArray();
        if (stages.Any(s => s.FreelancerId != null) || await db.EscrowFundingStages.AnyAsync(s => ids.Contains(s.MilestoneId) && s.ReservationActive, ct))
            throw new InvalidOperationException("A selected milestone is already reserved or assigned.");
        if (await db.Escrows.AnyAsync(e => e.ProjectId == projectId && e.FreelancerId == proposal.FreelancerId, ct))
            throw new InvalidOperationException("Use this freelancer's existing contract to fund further work.");
        var parties = await db.Users.Where(u => u.Id == clientId || u.Id == proposal.FreelancerId).ToListAsync(ct);
        var client = parties.Single(u => u.Id == clientId);
        var freelancer = parties.Single(u => u.Id == proposal.FreelancerId);
        if (parties.Any(u => u.WalletVerifiedAt == null || string.IsNullOrWhiteSpace(u.WalletAddressNormalized)))
            throw new InvalidOperationException("Both parties must verify their own wallets before funding.");
        var config = options.Value;
        var addresses = new[] { client.WalletAddressNormalized!, freelancer.WalletAddressNormalized!, config.ArbiterAddress.ToLowerInvariant() };
        if (addresses.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
            throw new InvalidOperationException("Client, freelancer and arbiter must use three different wallets.");
        if (string.IsNullOrWhiteSpace(config.FeeRecipientAddress) || !Nethereum.Util.AddressUtil.Current.IsValidEthereumAddressHexFormat(config.FeeRecipientAddress))
            throw new InvalidOperationException("The platform fee recipient is not configured.");
        var escrow = new Escrow {
            ProjectId = projectId, FreelancerId = proposal.FreelancerId, ProtocolVersion = 3,
            ChainId = config.ChainId, TokenAddress = config.TokenAddress.ToLowerInvariant(),
            ClientWalletAddress = client.WalletAddress!, FreelancerWalletAddress = freelancer.WalletAddress!,
            ArbiterWalletAddress = config.ArbiterAddress, FeeRecipientAddress = config.FeeRecipientAddress,
            PlatformFeeBasisPoints = EscrowFeePolicy.BasisPoints, TotalAmount = stages[0].Amount
        };
        db.Escrows.Add(escrow);
        for (var index = 0; index < stages.Count; index++)
            db.EscrowFundingStages.Add(NewStage(escrow, stages[index], index, index == 0 ? 0 : null, proposalId));
        // This is a reservation, not final acceptance or work in progress.
        proposal.Status = ProposalStatus.AwaitingFunding;
        stages[0].FreelancerId = proposal.FreelancerId;
        stages[0].OnChainIndex = 0;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return escrow.Id;
    }

    public static EscrowFundingStage NewStage(Escrow escrow, MileStone stage, int order, int? chainIndex, Guid? proposalId = null)
    {
        var reference = "0x" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var terms = JsonSerializer.Serialize(new { escrow.ProjectId, escrow.FreelancerId, escrow.ChainId,
            escrow.TokenAddress, escrow.ClientWalletAddress, escrow.FreelancerWalletAddress,
            escrow.ArbiterWalletAddress, escrow.FeeRecipientAddress, escrow.PlatformFeeBasisPoints,
            stage.Id, stage.Title, stage.Description, Amount = stage.Amount.ToString(CultureInfo.InvariantCulture), stage.Deadline, reference });
        return new EscrowFundingStage { EscrowId = escrow.Id, MilestoneId = stage.Id, ProposalId = proposalId,
            Title = stage.Title, TermsJson = terms,
            PlanOrder = order, ChainIndex = chainIndex, Amount = stage.Amount, FundingReference = reference,
            TermsHash = "0x" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(terms))).ToLowerInvariant() };
    }
}
