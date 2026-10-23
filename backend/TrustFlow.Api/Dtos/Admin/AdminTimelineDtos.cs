using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Admin;

public sealed class AdminTimelineQuery
{
    [Range(1, 100)] public int PageSize { get; init; } = 25;
    [RegularExpression("^(all|project|proposals|milestones|escrows|deliveries|conversations|disputes|admin)$")]
    public string Category { get; init; } = "all";
    [RegularExpression("^(recorded|missing)$")] public string Timing { get; init; } = "recorded";
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public Guid? ActorId { get; init; }
    [MaxLength(4096)] public string? Cursor { get; init; }
}

// Metadata only: bodies, statements, prompts, file contents and account emails stay in their scoped views.
public sealed record AdminTimelineEvent
{
    public int Code { get; init; }
    public Guid ResourceId { get; init; }
    public Guid? RelatedId { get; init; }
    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Label { get; init; }
    public DateTimeOffset? OccurredAt { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorName { get; init; }
    public string? TransactionHash { get; init; }
    public string? TransactionSender { get; init; }
    public long? BlockNumber { get; init; }
}

public sealed record AdminTimelinePage(Guid ProjectId, DateTimeOffset SnapshotAt,
    IReadOnlyList<AdminTimelineEvent> Items, string? NextCursor);
