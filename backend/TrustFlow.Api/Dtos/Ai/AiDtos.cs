using System.ComponentModel.DataAnnotations;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Ai;

public sealed class AiStatusResponse
{
    public bool Enabled { get; init; }

    // True while the fixed placeholder model is answering.
    public bool IsDemo { get; init; }
}

// ---- project drafting -------------------------------------------------

public sealed class ProjectDraftRequest
{
    [Required]
    [StringLength(3000, MinimumLength = 10)]
    public string Idea { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "1000000000")]
    [DecimalPrecision(18, 2)]
    public decimal? BudgetHint { get; init; }

    [Range(1, 730)]
    public int? DeadlineDays { get; init; }
}

public sealed class ProjectDraftResponse
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Budget { get; init; }

    public string BudgetExact => Budget.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string PriceRationale { get; init; } = string.Empty;

    public List<ProjectDraftMilestone> Milestones { get; init; } = [];

    public List<string> Assumptions { get; init; } = [];

    public List<string> QuestionsForClient { get; init; } = [];

    public bool IsDemo { get; init; }
}

public sealed class ProjectDraftMilestone
{
    public int SequenceNumber { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string AmountExact => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public int DueInDays { get; init; }
}

// ---- milestone review -------------------------------------------------

public sealed class AiCriterionResponse
{
    public string Requirement { get; init; } = string.Empty;

    // met, partially_met, not_met or unclear
    public string Status { get; init; } = "unclear";

    public string Evidence { get; init; } = string.Empty;
}

public sealed class MilestoneAiReviewResponse
{
    public Guid SubmissionId { get; init; }

    public int SubmissionVersion { get; init; }

    // Approve, RequestChanges or InsufficientEvidence
    public string Recommendation { get; init; } = string.Empty;

    public decimal Confidence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public List<AiCriterionResponse> Criteria { get; init; } = [];

    public string SuggestedFeedback { get; init; } = string.Empty;

    public List<string> Limitations { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsDemo { get; init; }
}

// ---- freelancer profile and resume ------------------------------------

public sealed class ProfileGapResponse
{
    public string Field { get; init; } = string.Empty;

    public string Advice { get; init; } = string.Empty;
}

public sealed class SuggestedPortfolioItemResponse
{
    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}

public sealed class ProfileCoachResponse
{
    public int CompletenessScore { get; init; }

    public string Summary { get; init; } = string.Empty;

    public List<ProfileGapResponse> Missing { get; init; } = [];

    public string SuggestedHeadline { get; init; } = string.Empty;

    public string SuggestedAbout { get; init; } = string.Empty;

    public List<string> SuggestedSkills { get; init; } = [];

    public List<SuggestedPortfolioItemResponse> SuggestedPortfolioItems
    {
        get;
        init;
    } = [];

    public bool IsDemo { get; init; }
}

public sealed class ResumeDraftRequest
{
    // Facts the profile and finished work do not show: past jobs,
    // education, anything the freelancer wants on the resume.
    [StringLength(3000)]
    public string? ExtraFacts { get; init; }
}

public sealed class ResumeDraftResponse
{
    public string ResumeMarkdown { get; init; } = string.Empty;

    public List<string> Notes { get; init; } = [];

    public bool IsDemo { get; init; }
}

// ---- disputes ---------------------------------------------------------

public sealed class OpenDisputeRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 20)]
    public string Statement { get; init; } = string.Empty;
}

public sealed class RespondToDisputeRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 20)]
    public string Statement { get; init; } = string.Empty;
}

public sealed class DisputeRulingResponse
{
    // FavorClient, FavorFreelancer or Split
    public string Verdict { get; init; } = string.Empty;

    public int FreelancerSharePercent { get; init; }

    public int ClientSharePercent { get; init; }

    public decimal FreelancerAmount { get; init; }

    public string FreelancerAmountExact => FreelancerAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public decimal ClientAmount { get; init; }

    public string ClientAmountExact => ClientAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public decimal Confidence { get; init; }

    public string Summary { get; init; } = string.Empty;

    public List<AiCriterionResponse> Findings { get; init; } = [];

    public string Reasoning { get; init; } = string.Empty;

    public List<string> ManipulationAttempts { get; init; } = [];

    public DateTimeOffset RuledAt { get; init; }
}

public sealed class DisputeResponse
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public Guid MilestoneId { get; init; }

    public string MilestoneTitle { get; init; } = string.Empty;

    public decimal MilestoneAmount { get; init; }

    public string MilestoneAmountExact => MilestoneAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Open, Ruled, Inconclusive or Settled
    public string Status { get; init; } = string.Empty;

    // Which side the viewer is: Client or Freelancer.
    public string MyRole { get; init; } = string.Empty;

    public string OpenedByRole { get; init; } = string.Empty;

    public string OpenerStatement { get; init; } = string.Empty;

    public string? RespondentStatement { get; init; }

    public DateTimeOffset? RespondentStatementAt { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    // After this either side may ask for a ruling without waiting for the
    // other statement.
    public DateTimeOffset ResponseDeadline { get; init; }

    public bool CanRespond { get; init; }

    public bool CanRequestRuling { get; init; }

    public DisputeRulingResponse? Ruling { get; init; }

    public bool IsDemo { get; init; }

    // Plain words on what a ruling is and is not.
    public string Notice { get; init; } = string.Empty;
}
