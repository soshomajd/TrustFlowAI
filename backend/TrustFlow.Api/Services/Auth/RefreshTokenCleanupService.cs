using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;

namespace TrustFlow.Api.Services.Auth;

public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CleanupInterval =
        TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await CleanupExpiredTokensAsync(
                stoppingToken
            );

            using var timer =
                new PeriodicTimer(CleanupInterval);

            while (await timer.WaitForNextTickAsync(
                       stoppingToken))
            {
                await CleanupExpiredTokensAsync(
                    stoppingToken
                );
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Application is shutting down normally.
        }
    }

    private async Task CleanupExpiredTokensAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope =
                scopeFactory.CreateScope();

            var dbContext =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var now = DateTimeOffset.UtcNow;

            var deletedTokens =
                await dbContext.RefreshTokens
                    .Where(refreshToken =>
                        refreshToken.ExpiresAt <= now)
                    .ExecuteDeleteAsync(
                        cancellationToken
                    );

            if (deletedTokens > 0)
            {
                logger.LogInformation(
                    "Deleted {DeletedTokens} expired refresh tokens.",
                    deletedTokens
                );
            }

            // Mailed codes expire within minutes, so a day of grace is
            // plenty before the rows are only clutter.
            var staleCodeCutoff = now.AddDays(-1);

            await dbContext.AuthCodes
                .Where(authCode =>
                    authCode.ExpiresAt <= staleCodeCutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "An error occurred while deleting expired refresh tokens."
            );
        }
    }
}