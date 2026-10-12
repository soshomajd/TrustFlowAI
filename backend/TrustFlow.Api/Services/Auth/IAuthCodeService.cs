using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Auth;

public enum AuthCodeCheck
{
    Valid,
    Invalid,
    Missing,
    Exhausted
}

public sealed record AuthCodeIssueResult(
    string? Code,
    TimeSpan? RetryAfter)
{
    public bool IsThrottled => Code is null;
}

public sealed record AuthCodeCheckResult(
    AuthCodeCheck Outcome,
    Guid CodeId);

public interface IAuthCodeService
{
    Task<AuthCodeIssueResult> IssueAsync(
        Guid userId,
        AuthCodePurpose purpose,
        CancellationToken cancellationToken);

    // Checking a code spends one attempt, so a guesser gets a handful of
    // tries per issued code no matter how many requests they send.
    Task<AuthCodeCheckResult> CheckAsync(
        Guid userId,
        AuthCodePurpose purpose,
        string code,
        CancellationToken cancellationToken);

    // True only for the caller that actually used the code, so two
    // concurrent requests cannot both succeed with it.
    Task<bool> ConsumeAsync(
        Guid codeId,
        CancellationToken cancellationToken);
}
