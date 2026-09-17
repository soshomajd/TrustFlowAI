namespace TrustFlow.Api.Models;

public sealed class MilestoneSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MilestoneId { get; set; }
    public MileStone Milestone { get; set; } = null!;
    public Guid FreelancerId { get; set; }
    public int Version { get; set; }
    public string Note { get; set; } = string.Empty;
    public string? PreviewLink { get; set; }
    public string? SourceFilesLink { get; set; }
    public string? ClientFeedback { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReviewedAt { get; set; }
    public List<MilestoneSubmissionAttachment> Attachments { get; set; } = [];
}
