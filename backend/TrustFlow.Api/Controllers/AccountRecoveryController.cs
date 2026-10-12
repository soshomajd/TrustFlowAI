using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Auth;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Auth;

namespace TrustFlow.Api.Controllers;

// Proving ownership of an email address: confirming a new account and
// resetting a forgotten password. Both ride on a short mailed code.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.AccountRecovery)]
public class AccountRecoveryController(
    UserManager<ApplicationUser> userManager,
    IAuthCodeService authCodes,
    IAccountEmailService accountEmails,
    AppDbContext dbContext)
    : ControllerBase
{
    // One answer for every way a code can be wrong, so the response does
    // not reveal which addresses have an account.
    private const string InvalidCodeMessage =
        "That code is not valid or has expired.";

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return InvalidCode();
        }

        var check = await authCodes.CheckAsync(
            user.Id,
            AuthCodePurpose.EmailVerification,
            request.Code,
            cancellationToken);

        if (check.Outcome != AuthCodeCheck.Valid ||
            !await authCodes.ConsumeAsync(
                check.CodeId,
                cancellationToken))
        {
            return CodeFailure(check.Outcome);
        }

        user.EmailConfirmed = true;

        var update = await userManager.UpdateAsync(user);

        if (!update.Succeeded)
        {
            return BadRequest(new
            {
                errors = update.Errors
                    .Select(error => error.Description)
            });
        }

        return Ok(new
        {
            message = "Your email is verified. You can sign in now."
        });
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        // An unknown or already verified address gets the same answer as a
        // real one, so this cannot be used to look accounts up.
        if (user is null || user.EmailConfirmed)
        {
            return Accepted(GenericSentResponse());
        }

        var result = await accountEmails
            .SendEmailVerificationAsync(user, cancellationToken);

        return result switch
        {
            AccountEmailResult.Throttled => TooManyRequests(),

            AccountEmailResult.Failed => StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    code = "email_unavailable",
                    message =
                        "We could not send the email right now. " +
                        "Try again in a minute."
                }),

            _ => Accepted(GenericSentResponse())
        };
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is not null)
        {
            // The outcome is deliberately not reported. A throttled or
            // failed send looks the same as a successful one, so this
            // endpoint cannot confirm that an address has an account.
            await accountEmails.SendPasswordResetAsync(
                user,
                cancellationToken);
        }

        return Accepted(GenericSentResponse());
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return InvalidCode();
        }

        // The password rules are checked before the code is spent, so a
        // weak password costs the user a retry rather than a new code.
        var passwordErrors = new List<string>();

        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(
                userManager,
                user,
                request.NewPassword);

            if (!validation.Succeeded)
            {
                passwordErrors.AddRange(
                    validation.Errors
                        .Select(error => error.Description));
            }
        }

        if (passwordErrors.Count > 0)
        {
            return BadRequest(new { errors = passwordErrors });
        }

        var check = await authCodes.CheckAsync(
            user.Id,
            AuthCodePurpose.PasswordReset,
            request.Code,
            cancellationToken);

        if (check.Outcome != AuthCodeCheck.Valid ||
            !await authCodes.ConsumeAsync(
                check.CodeId,
                cancellationToken))
        {
            return CodeFailure(check.Outcome);
        }

        var resetToken = await userManager
            .GeneratePasswordResetTokenAsync(user);

        var reset = await userManager.ResetPasswordAsync(
            user,
            resetToken,
            request.NewPassword);

        if (!reset.Succeeded)
        {
            return BadRequest(new
            {
                errors = reset.Errors
                    .Select(error => error.Description)
            });
        }

        // Receiving the code proves the address, and a locked-out user is
        // exactly who needs this path to work.
        user.EmailConfirmed = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;

        await userManager.UpdateAsync(user);

        // Whoever held the old password, or an old session, is signed out.
        await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.UserId == user.Id &&
                refreshToken.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    refreshToken => refreshToken.RevokedAt,
                    (DateTimeOffset?)DateTimeOffset.UtcNow),
                cancellationToken);

        return Ok(new
        {
            message =
                "Your password was changed. Sign in with the new one."
        });
    }

    private static object GenericSentResponse() =>
        new
        {
            message =
                "If that address has an account, a code is on its way."
        };

    private IActionResult InvalidCode() =>
        BadRequest(new
        {
            code = "invalid_code",
            message = InvalidCodeMessage
        });

    private IActionResult CodeFailure(AuthCodeCheck outcome) =>
        outcome == AuthCodeCheck.Exhausted
            ? BadRequest(new
            {
                code = "code_exhausted",
                message =
                    "Too many wrong attempts. Ask for a new code."
            })
            : InvalidCode();

    private IActionResult TooManyRequests()
    {
        var retryAfter = (int)Math.Ceiling(
            AuthCodeService.ResendCooldown.TotalSeconds);

        Response.Headers["Retry-After"] = retryAfter.ToString();

        return StatusCode(
            StatusCodes.Status429TooManyRequests,
            new
            {
                code = "resend_throttled",
                retryAfterSeconds = retryAfter,
                message =
                    "A code was sent a moment ago. " +
                    "Wait a minute before asking for another."
            });
    }
}
