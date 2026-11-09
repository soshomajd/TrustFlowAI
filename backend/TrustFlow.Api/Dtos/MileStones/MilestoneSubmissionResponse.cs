namespace TrustFlow.Api.Dtos.Milestones;

public sealed class MilestoneSubmissionResponse
{
    public Guid Id { get; init; }
    public Guid MilestoneId { get; init; }
    public int Version { get; init; }
    public string Note { get; init; } = string.Empty;
    public string? PreviewLink { get; init; }
    public string? SourceFilesLink { get; init; }
    // The Client's payment entitlement; authors/reviewers have separate access.
    public bool OriginalsUnlocked { get; init; }
    public bool HasSourceFiles { get; init; }
    public string? ClientFeedback { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public List<MilestoneSubmissionAttachmentResponse> Attachments { get; init; } = [];
}

public sealed class MilestoneSubmissionAttachmentResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public int FileSize { get; init; }
    public string Sha256 { get; init; } = string.Empty;
    public bool HasPreview { get; init; }
}
