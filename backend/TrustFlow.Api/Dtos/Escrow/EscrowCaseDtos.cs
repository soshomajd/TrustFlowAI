using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Dtos.Milestones;

namespace TrustFlow.Api.Dtos.Escrows;

// What the client or the freelancer of a project sees about a case. The
// model's advice to the reviewer is deliberately not part of it.
public sealed class EscrowCaseResponse
{
    public Guid Id { get; init; }

    public string ReferenceCode => TrustFlow.Api.Models.EscrowCaseReference.Format(Id);

    public Guid EscrowId { get; init; }

    public Guid ProjectId { get; init; }

    public Guid FreelancerId { get; init; }

    public Guid MilestoneId { get; init; }

    public string MilestoneTitle { get; init; } = string.Empty;

    public decimal MilestoneAmount { get; init; }

    public string MilestoneAmountExact => MilestoneAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // "PaymentRequest" or "RefundRequest".
    public string Kind { get; init; } = string.Empty;

    // Which side opened it: "Freelancer" or "Client".
    public string OpenedByRole { get; init; } = string.Empty;

    // "Open", "Withdrawn" or "Settled".
    public string Status { get; init; } = string.Empty;

    public DateTimeOffset OpenedAt { get; init; }

    // From this moment the reviewer may settle the case.
    public DateTimeOffset SettleableAt { get; init; }

    // From this moment either side can force a 50/50 split, if the
    // reviewer never acted.
    public DateTimeOffset StallableAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    public string? SettlementTransactionHash { get; init; }
    public DateTimeOffset? SettlementAt { get; init; }

    // Gross award before the fixed platform fee.
    public decimal? PaidToFreelancer { get; init; }
    public string? PaidToFreelancerExact => PaidToFreelancer?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal PlatformFeePaid { get; init; }
    public string PlatformFeePaidExact => PlatformFeePaid.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal? FreelancerNetPaid { get; init; }
    public string? FreelancerNetPaidExact => FreelancerNetPaid?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public decimal? ReturnedToClient { get; init; }

    public string? ReturnedToClientExact => ReturnedToClient?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool EscrowEnded { get; init; }

    public string? ReviewNote { get; init; }

    public long ChainId { get; init; }

    public string? ContractAddress { get; init; }

    public string? ArbiterWalletAddress { get; init; }
    public int PlatformFeeBasisPoints { get; init; }
}

public sealed class ReviewCaseSummaryResponse
{
    public Guid Id { get; init; }

    public string ReferenceCode => TrustFlow.Api.Models.EscrowCaseReference.Format(Id);

    public Guid ProjectId { get; init; }

    public string ProjectTitle { get; init; } = string.Empty;

    public string MilestoneTitle { get; init; } = string.Empty;

    public decimal MilestoneAmount { get; init; }

    public string MilestoneAmountExact => MilestoneAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string Kind { get; init; } = string.Empty;

    public string OpenedByRole { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset SettleableAt { get; init; }

    // Open, and the 14 days are over.
    public bool IsReady { get; init; }

    public bool HasAdvice { get; init; }

    public bool EscrowEnded { get; init; }

    // Gross award before the fixed platform fee.
    public decimal? PaidToFreelancer { get; init; }
    public string? PaidToFreelancerExact => PaidToFreelancer?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal PlatformFeePaid { get; init; }
    public string PlatformFeePaidExact => PlatformFeePaid.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal? FreelancerNetPaid { get; init; }
    public string? FreelancerNetPaidExact => FreelancerNetPaid?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public decimal? ReturnedToClient { get; init; }

    public string? ReturnedToClientExact => ReturnedToClient?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public DateTimeOffset? ClosedAt { get; init; }

    public string? SettlementTransactionHash { get; init; }
    public DateTimeOffset? SettlementAt { get; init; }
}

public sealed class ReviewCaseAdviceResponse
{
    public int FreelancerSharePercent { get; init; }

    public decimal Confidence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<AiCriterionResponse> Findings { get; init; } = [];

    public string Reasoning { get; init; } = string.Empty;

    public IReadOnlyList<string> ManipulationAttempts { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsDemo { get; init; }
}

public sealed class ReviewCaseStatementsResponse
{
    public string OpenedByRole { get; init; } = string.Empty;

    public string OpenerStatement { get; init; } = string.Empty;

    public string? RespondentStatement { get; init; }

    public string Status { get; init; } = string.Empty;

    // The model's opinion that a party asked for earlier, if any.
    public int? RulingFreelancerSharePercent { get; init; }

    public string? RulingSummary { get; init; }
}

public sealed class ReviewCaseDetailResponse
{
    public ReviewCaseSummaryResponse Case { get; init; } = null!;

    public string ProjectDescription { get; init; } = string.Empty;

    public string MilestoneDescription { get; init; } = string.Empty;

    public int MilestoneSequence { get; init; }

    public DateTimeOffset MilestoneDeadline { get; init; }

    public bool DeadlinePassed { get; init; }

    public string MilestoneStatus { get; init; } = string.Empty;

    public long ChainId { get; init; }

    public string? ContractAddress { get; init; }

    public string? ArbiterWalletAddress { get; init; }
    public int PlatformFeeBasisPoints { get; init; }

    public string TokenAddress { get; init; } = string.Empty;

    public int TokenDecimals { get; init; }

    // The milestone's position on the contract, which is what settleCase
    // takes as its first argument.
    public int OnChainMilestoneIndex { get; init; }

    public IReadOnlyList<MilestoneSubmissionResponse> Deliveries { get; init; } = [];

    public ReviewCaseStatementsResponse? Dispute { get; init; }

    public ReviewCaseAdviceResponse? Advice { get; init; }

    public string? ReviewNote { get; init; }

    public bool AiEnabled { get; init; }
}

public sealed class SaveReviewNoteRequest
{
    public string Note { get; init; } = string.Empty;
}
