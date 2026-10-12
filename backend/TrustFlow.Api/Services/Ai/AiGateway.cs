using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Ai;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Ai;

public sealed record AiGatewayResult<T>(
    T Value,
    Guid RunId,
    string OutputJson);

public sealed record AiStatusInfo(
    bool Enabled,
    string Provider,
    bool IsDemo);

// Every call to a model goes through here and nowhere else: the switch,
// the per-user budget, the audit row, and the rule that the answer must
// parse into the shape the feature expects before anyone sees it.
public interface IAiGateway
{
    AiStatusInfo Status { get; }

    Task<AiGatewayResult<T>> RunAsync<T>(
        Guid userId,
        AiPurpose purpose,
        string systemPrompt,
        IReadOnlyList<AiContentBlock> content,
        AiToolSchema tool,
        Func<JsonObject, T> parse,
        CancellationToken cancellationToken);
}

public sealed class AiGateway(
    AppDbContext dbContext,
    IAiModel model,
    IOptions<AiOptions> options,
    ILogger<AiGateway> logger)
    : IAiGateway
{
    public AiStatusInfo Status => new(
        options.Value.Enabled,
        options.Value.Provider,
        options.Value.IsMock);

    public async Task<AiGatewayResult<T>> RunAsync<T>(
        Guid userId,
        AiPurpose purpose,
        string systemPrompt,
        IReadOnlyList<AiContentBlock> content,
        AiToolSchema tool,
        Func<JsonObject, T> parse,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            throw new AiUnavailableException();
        }

        var since = DateTimeOffset.UtcNow.AddHours(-24);

        var recentRuns = await dbContext.AiRuns
            .AsNoTracking()
            .CountAsync(
                run => run.UserId == userId && run.CreatedAt > since,
                cancellationToken);

        if (recentRuns >= settings.DailyRunsPerUser)
        {
            throw new AiQuotaExceededException(
                settings.DailyRunsPerUser);
        }

        var modelName = settings.IsMock
            ? "mock"
            : purpose == AiPurpose.DisputeRuling
                ? settings.EffectiveDisputeModel
                : settings.Model;

        var run = new AiRun
        {
            UserId = userId,
            Purpose = purpose,
            Provider = settings.Provider,
            Model = modelName,

            // Failed until proven otherwise, so an exception nobody
            // anticipated can never be recorded as a success.
            Status = AiRunStatus.Failed,
            ErrorCode = "unexpected"
        };

        var timer = Stopwatch.StartNew();

        try
        {
            var response = await model.CompleteAsync(
                new AiModelRequest(
                    purpose,
                    modelName,
                    systemPrompt,
                    content,
                    tool),
                cancellationToken);

            T value;

            try
            {
                value = parse(response.Output);
            }
            catch (Exception exception)
                when (exception is not AiModelException &&
                      exception is not OperationCanceledException)
            {
                // The model answered, but not in the shape the feature
                // needs. That is a failed run, never a half-trusted one.
                throw new AiModelException(
                    "invalid_output",
                    "The AI answer was not in the expected form.",
                    exception);
            }

            run.Status = AiRunStatus.Succeeded;
            run.ErrorCode = null;
            run.InputTokens = response.InputTokens;
            run.OutputTokens = response.OutputTokens;
            run.OutputJson = response.Output.ToJsonString();

            return new AiGatewayResult<T>(
                value,
                run.Id,
                run.OutputJson);
        }
        catch (AiModelException exception)
        {
            run.Status = AiRunStatus.Failed;
            run.ErrorCode = exception.Code;

            logger.LogWarning(
                exception,
                "AI run for {Purpose} failed with {Code}.",
                purpose,
                exception.Code);

            throw;
        }
        finally
        {
            timer.Stop();
            run.DurationMilliseconds = (int)timer.ElapsedMilliseconds;

            // A cancelled request never reached an answer, so it leaves no
            // row. Every other outcome is recorded.
            if (!cancellationToken.IsCancellationRequested)
            {
                dbContext.AiRuns.Add(run);

                await dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
