using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.IntegrationTests.Tests;

// The Anthropic client cannot be run for real without a paid key, so this
// pins what it sends and how it reads the answer against a stand-in server.
public sealed class AnthropicAiModelTests
{
    private const string Key = "sk-ant-test-key-1234567890";

    private static readonly AiToolSchema Tool = new(
        "submit_answer",
        "Return the answer.",
        AiSchema.Object([("note", AiSchema.Text("A note."))], "note"));

    [Fact]
    public async Task SendsOneForcedToolCallToTheMessagesEndpoint()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, Answer()));

        var model = NewModel(handler);

        var response = await model.CompleteAsync(
            Request(
                new AiTextBlock("hello"),
                new AiImageBlock("image/png", [1, 2, 3])),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            "https://api.example.test/v1/messages",
            request.RequestUri!.ToString());
        Assert.Equal(Key, request.Headers.GetValues("x-api-key").Single());
        Assert.Equal(
            "2023-06-01",
            request.Headers.GetValues("anthropic-version").Single());

        var body = JsonNode.Parse(handler.Bodies.Single())!;

        Assert.Equal("claude-test", body["model"]!.GetValue<string>());
        Assert.Equal(4096, body["max_tokens"]!.GetValue<int>());
        Assert.Equal("system text", body["system"]!.GetValue<string>());

        // The model must answer through the one tool, never in free text.
        Assert.Equal("tool", body["tool_choice"]!["type"]!.GetValue<string>());
        Assert.Equal(
            "submit_answer",
            body["tool_choice"]!["name"]!.GetValue<string>());
        Assert.Equal(
            "submit_answer",
            body["tools"]![0]!["name"]!.GetValue<string>());

        var content = body["messages"]![0]!["content"]!.AsArray();

        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Equal("hello", content[0]!["text"]!.GetValue<string>());
        Assert.Equal("image", content[1]!["type"]!.GetValue<string>());
        Assert.Equal(
            "image/png",
            content[1]!["source"]!["media_type"]!.GetValue<string>());
        Assert.Equal(
            Convert.ToBase64String([1, 2, 3]),
            content[1]!["source"]!["data"]!.GetValue<string>());

        // Temperature is left out unless it is configured.
        Assert.Null(body["temperature"]);

        Assert.Equal("fine", response.Output["note"]!.GetValue<string>());
        Assert.Equal(11, response.InputTokens);
        Assert.Equal(7, response.OutputTokens);
    }

    [Fact]
    public async Task SendsTemperatureOnlyWhenConfigured()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, Answer()));

        await NewModel(handler, new AiOptions
        {
            Provider = "Anthropic",
            ApiKey = Key,
            BaseUrl = "https://api.example.test",
            Temperature = 0.2
        }).CompleteAsync(Request(new AiTextBlock("x")), CancellationToken.None);

        Assert.Equal(
            0.2,
            JsonNode.Parse(handler.Bodies.Single())!["temperature"]!
                .GetValue<double>());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "provider_rate_limited")]
    [InlineData(HttpStatusCode.Unauthorized, "provider_auth")]
    [InlineData(HttpStatusCode.Forbidden, "provider_auth")]
    [InlineData(HttpStatusCode.InternalServerError, "provider_error")]
    [InlineData(HttpStatusCode.BadRequest, "provider_error")]
    public async Task ProviderErrorsBecomeStableCodes_ThatLeakNeitherKeyNorBody(
        HttpStatusCode status,
        string code)
    {
        var handler = new StubHandler(_ => Json(
            status,
            $"{{\"error\":\"echoed prompt text and {Key}\"}}"));

        var exception = await Assert.ThrowsAsync<AiModelException>(() =>
            NewModel(handler).CompleteAsync(
                Request(new AiTextBlock("x")),
                CancellationToken.None));

        Assert.Equal(code, exception.Code);
        Assert.DoesNotContain(Key, exception.Message);
        Assert.DoesNotContain("echoed prompt", exception.Message);
    }

    [Fact]
    public async Task AnAnswerCutOffByTheTokenLimitIsNotTrusted()
    {
        var handler = new StubHandler(_ => Json(
            HttpStatusCode.OK,
            Answer(stopReason: "max_tokens")));

        var exception = await Assert.ThrowsAsync<AiModelException>(() =>
            NewModel(handler).CompleteAsync(
                Request(new AiTextBlock("x")),
                CancellationToken.None));

        Assert.Equal("output_truncated", exception.Code);
    }

    [Theory]
    [InlineData("{\"content\":[{\"type\":\"text\",\"text\":\"free text\"}]}")]
    [InlineData("{\"content\":[{\"type\":\"tool_use\",\"name\":\"other\",\"input\":{}}]}")]
    [InlineData("{\"content\":[]}")]
    [InlineData("not json at all")]
    public async Task AnythingButTheExpectedToolCallIsRefused(string body)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));

        var exception = await Assert.ThrowsAsync<AiModelException>(() =>
            NewModel(handler).CompleteAsync(
                Request(new AiTextBlock("x")),
                CancellationToken.None));

        Assert.Equal("invalid_output", exception.Code);
    }

    [Fact]
    public async Task AnUnreachableServiceAndATimeoutAreReportedAsSuch()
    {
        var unreachable = await Assert.ThrowsAsync<AiModelException>(() =>
            NewModel(new StubHandler(
                    _ => throw new HttpRequestException("no route")))
                .CompleteAsync(
                    Request(new AiTextBlock("x")),
                    CancellationToken.None));

        Assert.Equal("provider_unreachable", unreachable.Code);

        var timeout = await Assert.ThrowsAsync<AiModelException>(() =>
            NewModel(new StubHandler(
                    _ => throw new TaskCanceledException("slow")))
                .CompleteAsync(
                    Request(new AiTextBlock("x")),
                    CancellationToken.None));

        Assert.Equal("provider_timeout", timeout.Code);
    }

    private static AiModelRequest Request(params AiContentBlock[] content) =>
        new(
            AiPurpose.ProjectDraft,
            "claude-test",
            "system text",
            content,
            Tool);

    private static string Answer(string stopReason = "tool_use") =>
        new JsonObject
        {
            ["stop_reason"] = stopReason,
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = "toolu_1",
                ["name"] = "submit_answer",
                ["input"] = new JsonObject { ["note"] = "fine" }
            }),
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 11,
                ["output_tokens"] = 7
            }
        }.ToJsonString();

    private static HttpResponseMessage Json(
        HttpStatusCode status,
        string body) =>
        new(status)
        {
            Content = new StringContent(
                body,
                Encoding.UTF8,
                "application/json")
        };

    private static AnthropicAiModel NewModel(
        StubHandler handler,
        AiOptions? options = null) =>
        new(
            new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(options ?? new AiOptions
            {
                Provider = "Anthropic",
                ApiKey = Key,
                BaseUrl = "https://api.example.test"
            }),
            NullLogger<AnthropicAiModel>.Instance);

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
        : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            Bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(
                    cancellationToken));

            return respond(request);
        }
    }
}
