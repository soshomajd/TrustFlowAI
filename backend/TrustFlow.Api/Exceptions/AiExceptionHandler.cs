using Microsoft.AspNetCore.Diagnostics;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.Exceptions;

// Turns the failures the AI gateway can raise into answers a person can act
// on. Anything else falls through to the general handler.
public sealed class AiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, code, message) = exception switch
        {
            AiUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "ai_unavailable",
                "AI features are switched off right now."),

            AiQuotaExceededException quota => (
                StatusCodes.Status429TooManyRequests,
                "ai_quota_exceeded",
                $"You have used your {quota.Limit} AI requests for the " +
                "last 24 hours. Try again later."),

            // The text of an AiModelException is written to be shown.
            AiModelException model => (
                StatusCodes.Status502BadGateway,
                model.Code,
                model.Message),

            _ => (0, string.Empty, string.Empty)
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            new { code, message },
            cancellationToken);

        return true;
    }
}
