using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models.Ai;

// One call to the model. Kept for quota counting and for answering "what did
// the AI say and what did it cost" later. The prompt itself is not stored:
// for rulings the evidence snapshot lives on the dispute.
public sealed class AiRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public AiPurpose Purpose { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public AiRunStatus Status { get; set; }

    public string? ErrorCode { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int DurationMilliseconds { get; set; }

    public string? OutputJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;
}
