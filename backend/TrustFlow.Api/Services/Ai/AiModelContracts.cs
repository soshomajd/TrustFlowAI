using System.Text.Json.Nodes;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Ai;

public abstract record AiContentBlock;

public sealed record AiTextBlock(string Text) : AiContentBlock;

public sealed record AiImageBlock(
    string MediaType,
    byte[] Data) : AiContentBlock;

// The one structured answer the model must give. The model is made to call
// this single tool, so its reply is JSON that can be validated, never free
// text that something downstream might act on.
public sealed record AiToolSchema(
    string Name,
    string Description,
    JsonObject InputSchema);

public sealed record AiModelRequest(
    AiPurpose Purpose,
    string Model,
    string SystemPrompt,
    IReadOnlyList<AiContentBlock> UserContent,
    AiToolSchema Tool);

public sealed record AiModelResponse(
    JsonObject Output,
    int InputTokens,
    int OutputTokens);

public interface IAiModel
{
    Task<AiModelResponse> CompleteAsync(
        AiModelRequest request,
        CancellationToken cancellationToken);
}

// A failure the caller can describe to the user without leaking provider
// detail. The code is stable and machine readable.
public sealed class AiModelException(
    string code,
    string message,
    Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}

public sealed class AiUnavailableException()
    : Exception("AI features are switched off.");

public sealed class AiQuotaExceededException(int limit)
    : Exception(
        $"The daily limit of {limit} AI requests was reached.")
{
    public int Limit { get; } = limit;
}
