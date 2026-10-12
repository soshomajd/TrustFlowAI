using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models.Ai;

// The model's reading of one delivery against what the client asked for. It
// is the client's aid; approving or asking for changes stays their call.
public sealed class MilestoneAiReview
{
    public Guid SubmissionId { get; set; }

    public MilestoneSubmission Submission { get; set; } = null!;

    public MilestoneReviewRecommendation Recommendation { get; set; }

    public decimal Confidence { get; set; }

    public string ReviewJson { get; set; } = "{}";

    public Guid? AiRunId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;
}
