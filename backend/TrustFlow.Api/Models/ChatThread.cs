using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Models;

// One conversation between a project's client and one freelancer who applied
// to it or was given some of its milestones. A project with three freelancers
// has three threads, so what is said to one is never shown to another.
public sealed class ChatThread
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }

    [JsonIgnore]
    public Project Project { get; set; } = null!;

    public Guid FreelancerId { get; set; }

    [JsonIgnore]
    public ApplicationUser Freelancer { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;

    // Everything sent up to these times has been seen by that side. Unread
    // counts are worked out from them, so there is no per message flag to
    // keep in step.
    public DateTimeOffset ClientLastReadAt { get; set; } = DateTimeOffset.UnixEpoch;

    public DateTimeOffset FreelancerLastReadAt { get; set; } = DateTimeOffset.UnixEpoch;

    [JsonIgnore]
    public ICollection<ChatMessage> Messages { get; set; } = [];
}

// Plain text only. Messages are never edited or deleted, so what each side
// saw is what the record shows if the two later disagree about it.
public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ThreadId { get; set; }

    [JsonIgnore]
    public ChatThread Thread { get; set; } = null!;

    public Guid SenderId { get; set; }

    [JsonIgnore]
    public ApplicationUser Sender { get; set; } = null!;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
}
