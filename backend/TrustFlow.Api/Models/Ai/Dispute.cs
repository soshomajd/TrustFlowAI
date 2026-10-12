using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Models.Ai;

// A disagreement over one milestone. The parties each give one written
// statement and the model rules from the work and the requirement. There is
// no conversation: nobody can answer the model or argue with it.
public sealed class Dispute
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    public Guid MilestoneId { get; set; }

    public MileStone Milestone { get; set; } = null!;

    public Guid OpenedByUserId { get; set; }

    public Guid RespondentUserId { get; set; }

    // "Client" or "Freelancer": which side opened it.
    public string OpenedByRole { get; set; } = string.Empty;

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    public string OpenerStatement { get; set; } = string.Empty;

    public string? RespondentStatement { get; set; }

    public DateTimeOffset? RespondentStatementAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    // Set the moment a ruling starts, so two requests cannot both run one.
    public DateTimeOffset? RulingStartedAt { get; set; }

    public int RulingAttempts { get; set; }

    public DisputeVerdict? Verdict { get; set; }

    // Share of the milestone amount the freelancer should receive, 0-100.
    public int? FreelancerSharePercent { get; set; }

    public decimal? Confidence { get; set; }

    public string? RulingJson { get; set; }

    // Hash of exactly what the model was shown, so the ruling can be tied
    // to its evidence.
    public string? EvidenceSha256 { get; set; }

    public Guid? AiRunId { get; set; }

    public DateTimeOffset? RuledAt { get; set; }
}
