using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Ai;

// Talks to the Anthropic Messages API over plain HTTP, forcing the model to
// answer through one tool so the reply is structured JSON.
public sealed class AnthropicAiModel(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    ILogger<AnthropicAiModel> logger)
    : IAiModel
{
    private const string ApiVersion = "2023-06-01";

    public async Task<AiModelResponse> CompleteAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var content = new JsonArray();

        foreach (var block in request.UserContent)
        {
            content.Add(block switch
            {
                AiTextBlock text => new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = text.Text
                },

                AiImageBlock image => new JsonObject
                {
                    ["type"] = "image",
                    ["source"] = new JsonObject
                    {
                        ["type"] = "base64",
                        ["media_type"] = image.MediaType,
                        ["data"] = Convert.ToBase64String(image.Data)
                    }
                },

                _ => throw new InvalidOperationException(
                    "Unsupported content block.")
            });
        }

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = settings.MaxOutputTokens,
            ["system"] = request.SystemPrompt,
            ["messages"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = content
            }),
            ["tools"] = new JsonArray(new JsonObject
            {
                ["name"] = request.Tool.Name,
                ["description"] = request.Tool.Description,
                ["input_schema"] = request.Tool.InputSchema.DeepClone()
            }),
            ["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = request.Tool.Name
            }
        };

        if (settings.Temperature is { } temperature)
        {
            body["temperature"] = temperature;
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{settings.BaseUrl.TrimEnd('/')}/v1/messages")
        {
            Content = JsonContent.Create(body)
        };

        message.Headers.Add("x-api-key", settings.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);

        HttpResponseMessage response;

        try
        {
            response = await httpClient.SendAsync(
                message,
                cancellationToken);
        }
        catch (TaskCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiModelException(
                "provider_timeout",
                "The AI service took too long to answer.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new AiModelException(
                "provider_unreachable",
                "The AI service could not be reached.",
                exception);
        }

        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // The body can echo the prompt, so it is trimmed and kept
                // out of anything the user sees.
                logger.LogError(
                    "Anthropic returned {Status}: {Body}",
                    (int)response.StatusCode,
                    text.Length > 500 ? text[..500] : text);

                throw response.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        new AiModelException(
                            "provider_rate_limited",
                            "The AI service is busy. Try again soon."),

                    HttpStatusCode.Unauthorized or
                        HttpStatusCode.Forbidden =>
                        new AiModelException(
                            "provider_auth",
                            "The AI service rejected this server's key."),

                    _ => new AiModelException(
                        "provider_error",
                        "The AI service returned an error.")
                };
            }

            return Parse(text, request.Tool.Name);
        }
    }

    internal static AiModelResponse Parse(
        string responseBody,
        string toolName)
    {
        JsonNode? root;

        try
        {
            root = JsonNode.Parse(responseBody);
        }
        catch (JsonException exception)
        {
            throw new AiModelException(
                "invalid_output",
                "The AI service sent a reply that could not be read.",
                exception);
        }

        var stopReason = root?["stop_reason"]?.GetValue<string>();

        if (stopReason == "max_tokens")
        {
            throw new AiModelException(
                "output_truncated",
                "The answer was cut off. Try again with less input.");
        }

        var toolUse = root?["content"]?.AsArray()
            .FirstOrDefault(block =>
                block?["type"]?.GetValue<string>() == "tool_use" &&
                block["name"]?.GetValue<string>() == toolName);

        if (toolUse?["input"] is not JsonObject output)
        {
            throw new AiModelException(
                "invalid_output",
                "The AI service did not give a structured answer.");
        }

        return new AiModelResponse(
            output,
            root?["usage"]?["input_tokens"]?.GetValue<int>() ?? 0,
            root?["usage"]?["output_tokens"]?.GetValue<int>() ?? 0);
    }
}
