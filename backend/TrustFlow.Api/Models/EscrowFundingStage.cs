namespace TrustFlow.Api.Models;

// A reserved plan entry is not a deposit. Only canonical funding/start receipts
// promote it to funded/active; the chain index is explicit, never inferred from
// the order of unrelated project milestones.
public sealed class EscrowFundingStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EscrowId { get; set; }
    public Escrow Escrow { get; set; } = null!;
    public Guid MilestoneId { get; set; }
    public MileStone Milestone { get; set; } = null!;
    public Guid? ProposalId { get; set; }
    public int PlanOrder { get; set; }
    public int? ChainIndex { get; set; }
    public decimal Amount { get; set; }
    public string FundingReference { get; set; } = "";
    public string TermsHash { get; set; } = "";
    public string Title { get; set; } = "";
    public string TermsJson { get; set; } = "";
    public bool ReservationActive { get; set; } = true;
    public string? FundingTransactionHash { get; set; }
    public DateTimeOffset? FundedAt { get; set; }
    public string? StartTransactionHash { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
}
