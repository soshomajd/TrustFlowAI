using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Email;

namespace TrustFlow.Api.Services.Auth;

public sealed record AdminMfaPrompt(bool MfaRequired, string ChallengeToken, string MaskedEmail, DateTimeOffset ExpiresAt);
public sealed class AdminMfaDeliveryException(bool throttled) : Exception("Administrator email verification is unavailable.")
{
    public bool Throttled { get; } = throttled;
}

public sealed class AdminMfaService(AppDbContext db, UserManager<ApplicationUser> users, IOptions<JwtSettings> jwt,
    IAccountEmailService accountEmails, IAuthCodeService codes, IEmailDelivery delivery)
{
    public bool CanDeliverEmail => delivery is not LogEmailDelivery;

    public async Task<AdminMfaPrompt> StartAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (!CanDeliverEmail) throw new AdminMfaDeliveryException(false);
        var stamp = user.SecurityStamp;
        var sent = await accountEmails.SendAdminSignInAsync(user, cancellationToken);
        if (sent != AccountEmailResult.Sent) throw new AdminMfaDeliveryException(sent == AccountEmailResult.Throttled);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(user.Id, cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        if (stamp != user.SecurityStamp || !user.EmailConfirmed || await users.IsLockedOutAsync(user)
            || !await users.IsInRoleAsync(user, AppRoles.Admin)) throw new InvalidOperationException("Sign in again to begin administrator verification.");
        var now = DateTimeOffset.UtcNow;
        await db.AdminMfaChallenges.Where(item => item.UserId == user.Id && item.CreatedAt < now.AddDays(-1)).ExecuteDeleteAsync(cancellationToken);
        await db.AdminMfaChallenges.Where(item => item.UserId == user.Id && item.CompletedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ExpiresAt, now), cancellationToken);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = now.AddMinutes(5);
        db.AdminMfaChallenges.Add(new AdminMfaChallenge
        {
            UserId = user.Id, TokenHash = Hash(token), SecurityStamp = user.SecurityStamp!, CreatedAt = now, ExpiresAt = expires
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var email = user.Email!;
        return new AdminMfaPrompt(true, token, email[..1] + "***" + email[email.IndexOf('@')..], expires);
    }

    public async Task<ApplicationUser?> VerifyAsync(string token, string enteredCode, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var found = await db.AdminMfaChallenges.AsNoTracking().SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (found is null) return null;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockUserAsync(found.UserId, cancellationToken);
        var challenge = await db.AdminMfaChallenges.SingleAsync(item => item.Id == found.Id, cancellationToken);
        var user = await users.FindByIdAsync(found.UserId.ToString());
        var now = DateTimeOffset.UtcNow;
        if (user is null || challenge.CompletedAt != null || challenge.ExpiresAt <= now || challenge.Attempts >= 5
            || user.SecurityStamp != challenge.SecurityStamp || !user.EmailConfirmed || await users.IsLockedOutAsync(user)
            || !await users.IsInRoleAsync(user, AppRoles.Admin)) return null;
        challenge.Attempts++;
        var code = enteredCode.Trim();
        var check = await codes.CheckAsync(user.Id, AuthCodePurpose.AdminSignIn, code, cancellationToken);
        if (check.Outcome != AuthCodeCheck.Valid || !await codes.ConsumeAsync(check.CodeId, cancellationToken))
        {
            Ensure(await users.AccessFailedAsync(user));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (!user.TwoFactorEnabled) Ensure(await users.SetTwoFactorEnabledAsync(user, true));
        Ensure(await users.ResetAccessFailedCountAsync(user));
        challenge.CompletedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    private Task<int> LockUserAsync(Guid userId, CancellationToken cancellationToken) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE", cancellationToken);
    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(jwt.Value.Key), Encoding.UTF8.GetBytes("admin-mfa:" + value)));
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Administrator verification settings could not be saved.");
    }
}
