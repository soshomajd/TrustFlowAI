using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Email;

namespace TrustFlow.Api.Services.Auth;

public enum AccountEmailResult
{
    Sent,
    Throttled,
    Failed
}

public interface IAccountEmailService
{
    Task<AccountEmailResult> SendEmailVerificationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken);

    Task<AccountEmailResult> SendPasswordResetAsync(
        ApplicationUser user,
        CancellationToken cancellationToken);
}

public sealed class AccountEmailService(
    IAuthCodeService codes,
    IEmailDelivery delivery,
    ILogger<AccountEmailService> logger)
    : IAccountEmailService
{
    private static readonly int LifetimeMinutes =
        (int)AuthCodeService.Lifetime.TotalMinutes;

    public Task<AccountEmailResult> SendEmailVerificationAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            user,
            AuthCodePurpose.EmailVerification,
            "Verify your TrustFlow email",
            code =>
                $"Hi {user.FullName},{Environment.NewLine}{Environment.NewLine}" +
                $"Your TrustFlow verification code is:{Environment.NewLine}{Environment.NewLine}" +
                $"    {code}{Environment.NewLine}{Environment.NewLine}" +
                $"It works for {LifetimeMinutes} minutes. " +
                "If you did not create a TrustFlow account, ignore this email.",
            cancellationToken);
    }

    public Task<AccountEmailResult> SendPasswordResetAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            user,
            AuthCodePurpose.PasswordReset,
            "Reset your TrustFlow password",
            code =>
                $"Hi {user.FullName},{Environment.NewLine}{Environment.NewLine}" +
                $"Your TrustFlow password reset code is:{Environment.NewLine}{Environment.NewLine}" +
                $"    {code}{Environment.NewLine}{Environment.NewLine}" +
                $"It works for {LifetimeMinutes} minutes. " +
                "If you did not ask to reset your password, ignore this email " +
                "and your password will stay as it is.",
            cancellationToken);
    }

    private async Task<AccountEmailResult> SendAsync(
        ApplicationUser user,
        AuthCodePurpose purpose,
        string subject,
        Func<string, string> buildBody,
        CancellationToken cancellationToken)
    {
        var issued = await codes.IssueAsync(
            user.Id,
            purpose,
            cancellationToken);

        if (issued.IsThrottled)
        {
            return AccountEmailResult.Throttled;
        }

        try
        {
            await delivery.SendAsync(
                user.Email!,
                user.FullName,
                subject,
                buildBody(issued.Code!),
                cancellationToken);

            return AccountEmailResult.Sent;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The caller decides what the user is told. The address and
            // code stay out of the log line on purpose.
            logger.LogError(
                exception,
                "Sending a {Purpose} email failed for user {UserId}.",
                purpose,
                user.Id);

            return AccountEmailResult.Failed;
        }
    }
}
