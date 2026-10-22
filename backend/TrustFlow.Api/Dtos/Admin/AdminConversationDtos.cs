using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Admin;

public sealed record AdminConversationThread(Guid Id, Guid? ClientId, string ClientName,
    Guid FreelancerId, string FreelancerName, DateTimeOffset CreatedAt, DateTimeOffset LastMessageAt, int MessageCount);

public sealed class AdminConversationMessageQuery
{
    [Range(1, 100)]
    public int PageSize { get; init; } = 50;
    public Guid? BeforeMessageId { get; init; }
    public Guid? ThroughMessageId { get; init; }
}

public sealed record AdminConversationMessage(Guid Id, Guid SenderId, string SenderName, string SenderRole,
    string Body, DateTimeOffset SentAt);

public sealed record AdminConversationMessages(Guid ThreadId, Guid? ThroughMessageId, bool HasOlderMessages,
    Guid? NextBeforeMessageId, IReadOnlyList<AdminConversationMessage> Messages);
