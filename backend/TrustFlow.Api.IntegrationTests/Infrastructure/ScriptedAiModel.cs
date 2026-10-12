using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

// Stands in for the model. By default it answers like the demo model, and a
// test can replace the answer, slow it down, or make it fail. Every request
// is kept so a test can check exactly what the model would have been shown.
public sealed class ScriptedAiModel : IAiModel
{
    private static readonly MockAiModel Default = new();

    public Func<AiModelRequest, Task<JsonObject>>? Handler { get; set; }

    public ConcurrentQueue<AiModelRequest> Requests { get; } = new();

    public int Calls => Requests.Count;

    public void Reset()
    {
        Handler = null;
        Requests.Clear();
    }

    public async Task<AiModelResponse> CompleteAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);

        var output = Handler is null
            ? (await Default.CompleteAsync(request, cancellationToken))
                .Output
            : await Handler(request);

        return new AiModelResponse(output, 10, 20);
    }

    // Everything written in the prompt, as one string, for assertions.
    public static string AllText(AiModelRequest request) =>
        string.Join(
            "\n",
            request.UserContent
                .OfType<AiTextBlock>()
                .Select(block => block.Text)
                .Prepend(request.SystemPrompt));
}
