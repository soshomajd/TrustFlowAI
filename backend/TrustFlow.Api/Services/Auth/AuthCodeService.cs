using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Auth;

public sealed class AuthCodeService(
    AppDbContext dbContext,
    IOptions<JwtSettings> jwtSettings)
    : IAuthCodeService
{
    public const int MaxAttempts = 5;

    public static readonly TimeSpan Lifetime =
        TimeSpan.FromMinutes(15);

    public static readonly TimeSpan ResendCooldown =
        TimeSpan.FromSeconds(60);

    public async Task<AuthCodeIssueResult> IssueAsync(
        Guid userId,
        AuthCodePurpose purpose,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var latestCreatedAt = await dbContext.AuthCodes
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.Purpose == purpose)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => (DateTimeOffset?)item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestCreatedAt is not null &&
            now - latestCreatedAt.Value < ResendCooldown)
        {
            return new AuthCodeIssueResult(
                null,
                ResendCooldown - (now - latestCreatedAt.Value));
        }

        var code = RandomNumberGenerator
            .GetInt32(1_000_000)
            .ToString("D6");

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        try
        {
            // Older codes stop working the moment a new one is sent.
            await dbContext.AuthCodes
                .Where(item =>
                    item.UserId == userId &&
                    item.Purpose == purpose)
                .ExecuteDeleteAsync(cancellationToken);

            dbContext.AuthCodes.Add(new AuthCode
            {
                UserId = userId,
                Purpose = purpose,
                CodeHash = Hash(userId, purpose, code),
                CreatedAt = now,
                ExpiresAt = now + Lifetime
            });

            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request issued a code first. Treat this one as
            // throttled rather than leave two live codes behind.
            dbContext.ChangeTracker.Clear();

            return new AuthCodeIssueResult(null, ResendCooldown);
        }

        return new AuthCodeIssueResult(code, null);
    }

    public async Task<AuthCodeCheckResult> CheckAsync(
        Guid userId,
        AuthCodePurpose purpose,
        string code,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var live = await dbContext.AuthCodes
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId &&
                item.Purpose == purpose &&
                item.ConsumedAt == null &&
                item.ExpiresAt > now)
            .Select(item => new { item.Id, item.CodeHash })
            .FirstOrDefaultAsync(cancellationToken);

        if (live is null)
        {
            return new AuthCodeCheckResult(
                AuthCodeCheck.Missing,
                Guid.Empty);
        }

        // The attempt is claimed before the comparison and in one
        // statement, so parallel guesses cannot all slip past the limit.
        var claimed = await dbContext.AuthCodes
            .Where(item =>
                item.Id == live.Id &&
                item.ConsumedAt == null &&
                item.FailedAttempts < MaxAttempts)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.FailedAttempts,
                    item => item.FailedAttempts + 1),
                cancellationToken);

        if (claimed == 0)
        {
            return new AuthCodeCheckResult(
                AuthCodeCheck.Exhausted,
                live.Id);
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(live.CodeHash),
            Encoding.ASCII.GetBytes(
                Hash(userId, purpose, code.Trim())));

        return new AuthCodeCheckResult(
            matches ? AuthCodeCheck.Valid : AuthCodeCheck.Invalid,
            live.Id);
    }

    public async Task<bool> ConsumeAsync(
        Guid codeId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var updated = await dbContext.AuthCodes
            .Where(item =>
                item.Id == codeId &&
                item.ConsumedAt == null &&
                item.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.ConsumedAt,
                    (DateTimeOffset?)now),
                cancellationToken);

        return updated == 1;
    }

    // Keyed with the server signing secret and bound to the user and the
    // purpose, so a code for one flow or one account cannot be replayed in
    // another, and a leaked table cannot be brute-forced offline without
    // the key.
    private string Hash(
        Guid userId,
        AuthCodePurpose purpose,
        string code)
    {
        var key = Encoding.UTF8.GetBytes(
            jwtSettings.Value.Key);

        var payload = Encoding.UTF8.GetBytes(
            $"{userId:N}:{purpose}:{code}");

        return Convert.ToHexString(
                HMACSHA256.HashData(key, payload))
            .ToLowerInvariant();
    }
}
