using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Chat;

public static class ChatLimits
{
    public const int MessageLength = 2000;

    // The most recent messages a conversation shows when it opens.
    public const int OpeningPage = 200;
}

public sealed class SendChatMessageRequest
{
    [Required]
    [StringLength(ChatLimits.MessageLength)]
    public string Body { get; init; } = string.Empty;
}

public sealed class ChatMessageResponse
{
    public Guid Id { get; init; }

    public bool Mine { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTimeOffset SentAt { get; init; }
}

public sealed class ChatCounterpartResponse
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    // "Client" or "Freelancer": what they are on this project.
    public string Role { get; init; } = string.Empty;
}

public sealed class ChatConversationResponse
{
    public Guid ProjectId { get; init; }

    public string ProjectTitle { get; init; } = string.Empty;

    public Guid FreelancerId { get; init; }

    public ChatCounterpartResponse Counterpart { get; init; } = new();

    // False when the project has finished or the freelancer is no longer part
    // of it. The history stays readable.
    public bool CanSend { get; init; }

    public string? CannotSendReason { get; init; }

    public IReadOnlyList<ChatMessageResponse> Messages { get; init; } = [];

    public bool HasOlderMessages { get; init; }
}

public sealed class ChatInboxItemResponse
{
    public Guid ProjectId { get; init; }

    public string ProjectTitle { get; init; } = string.Empty;

    public Guid FreelancerId { get; init; }

    public ChatCounterpartResponse Counterpart { get; init; } = new();

    public string LastMessage { get; init; } = string.Empty;

    public bool LastMessageMine { get; init; }

    public DateTimeOffset LastMessageAt { get; init; }

    public int UnreadCount { get; init; }
}

public sealed class ChatInboxResponse
{
    public IReadOnlyList<ChatInboxItemResponse> Items { get; init; } = [];

    public int UnreadCount { get; init; }
}
