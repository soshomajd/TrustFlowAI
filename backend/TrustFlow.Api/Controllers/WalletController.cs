using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Wallets;
using TrustFlow.Api.Services;
using TrustFlow.Api.Validation;
using Nethereum.Signer;
using Npgsql;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public sealed class WalletController(
    AppDbContext dbContext)
    : ControllerBase
{
    [HttpPost("challenge")]
    public async Task<IActionResult>
        CreateChallenge(
            CreateWalletChallengeRequest request,
            CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (!Guid.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user identity."
            });
        }

        var walletAddress =
            request.WalletAddress.Trim();

        if (!EthereumAddressValidator.IsValid(
                walletAddress))
        {
            return BadRequest(new
            {
                message =
                    "A valid Ethereum wallet address is required."
            });
        }

        var normalizedWalletAddress =
            EthereumAddressValidator.Normalize(
                walletAddress
            );

        var user =
            await dbContext.Users
                .FirstOrDefaultAsync(
                    item => item.Id == userId,
                    cancellationToken
                );

        if (user is null)
        {
            return Unauthorized(new
            {
                message =
                    "User account could not be found."
            });
        }

        if (user.WalletAddressNormalized ==
            normalizedWalletAddress)
        {
            return Conflict(new
            {
                message =
                    "This wallet is already connected to your account."
            });
        }

        var walletAlreadyConnected =
            await dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.Id != userId &&
                        item.WalletAddressNormalized ==
                        normalizedWalletAddress,
                    cancellationToken
                );

        if (walletAlreadyConnected)
        {
            return Conflict(new
            {
                message =
                    "This wallet is already connected to another account."
            });
        }

        var nonce =
            Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32)
                )
                .ToLowerInvariant();

        var expiresAt =
       DateTimeOffset.FromUnixTimeSeconds(
           DateTimeOffset.UtcNow
               .AddMinutes(5)
               .ToUnixTimeSeconds()
       );

        user.PendingWalletAddress =
            walletAddress;

        user.WalletVerificationNonce =
            nonce;

        user.WalletVerificationExpiresAt =
            expiresAt;

        await dbContext.SaveChangesAsync(
            cancellationToken
        );

        var message =
            WalletVerificationMessageBuilder.Build(
                userId,
                walletAddress,
                nonce,
                expiresAt
            );

        return Ok(
            new WalletChallengeResponse
            {
                WalletAddress =
                    walletAddress,

                Message = message,

                ExpiresAt = expiresAt
            }
        );
    }
    [HttpPost("verify")]
    public async Task<IActionResult>
    VerifyWallet(
        VerifyWalletRequest request,
        CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (!Guid.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user identity."
            });
        }

        if (string.IsNullOrWhiteSpace(
                request.Signature))
        {
            return BadRequest(new
            {
                message =
                    "Wallet signature is required."
            });
        }

        var user =
            await dbContext.Users
                .FirstOrDefaultAsync(
                    item => item.Id == userId,
                    cancellationToken
                );

        if (user is null)
        {
            return Unauthorized(new
            {
                message =
                    "User account could not be found."
            });
        }

        if (
            string.IsNullOrWhiteSpace(
                user.PendingWalletAddress
            ) ||
            string.IsNullOrWhiteSpace(
                user.WalletVerificationNonce
            ) ||
            !user.WalletVerificationExpiresAt
                .HasValue
        )
        {
            return BadRequest(new
            {
                message =
                    "No active wallet challenge was found."
            });
        }

        var now =
            DateTimeOffset.UtcNow;

        if (
            user.WalletVerificationExpiresAt
                .Value <= now
        )
        {
            ClearWalletChallenge(user);

            await dbContext.SaveChangesAsync(
                cancellationToken
            );

            return BadRequest(new
            {
                message =
                    "The wallet challenge has expired."
            });
        }

        var pendingWalletAddress =
            user.PendingWalletAddress;

        var normalizedWalletAddress =
            EthereumAddressValidator.Normalize(
                pendingWalletAddress
            );

        var message =
            WalletVerificationMessageBuilder.Build(
                userId,
                pendingWalletAddress,
                user.WalletVerificationNonce,
                user.WalletVerificationExpiresAt
                    .Value
            );

        string recoveredAddress;

        try
        {
            var signer =
                new EthereumMessageSigner();

            recoveredAddress =
                signer.EncodeUTF8AndEcRecover(
                    message,
                    request.Signature.Trim()
                );
        }
        catch
        {
            return BadRequest(new
            {
                message =
                    "The wallet signature is invalid."
            });
        }

        if (
            !EthereumAddressValidator.IsValid(
                recoveredAddress
            ) ||
            EthereumAddressValidator.Normalize(
                recoveredAddress
            ) != normalizedWalletAddress
        )
        {
            return BadRequest(new
            {
                message =
                    "The signature was not created by the requested wallet."
            });
        }

        var walletAlreadyConnected =
            await dbContext.Users
                .AsNoTracking()
                .AnyAsync(
                    item =>
                        item.Id != userId &&
                        item.WalletAddressNormalized ==
                        normalizedWalletAddress,
                    cancellationToken
                );

        if (walletAlreadyConnected)
        {
            ClearWalletChallenge(user);

            await dbContext.SaveChangesAsync(
                cancellationToken
            );

            return Conflict(new
            {
                message =
                    "This wallet is already connected to another account."
            });
        }

        user.WalletAddress =
            pendingWalletAddress;

        user.WalletAddressNormalized =
            normalizedWalletAddress;

        user.WalletVerifiedAt =
            now;

        ClearWalletChallenge(user);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken
            );
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();

            return Conflict(new
            {
                message =
                    "This wallet is already connected to another account."
            });
        }

        return Ok(
            new WalletVerificationResponse
            {
                WalletAddress =
                    pendingWalletAddress,

                VerifiedAt = now
            }
        );
    }
    private static void ClearWalletChallenge(
    ApplicationUser user)
    {
        user.PendingWalletAddress = null;

        user.WalletVerificationNonce = null;

        user.WalletVerificationExpiresAt = null;
    }

    private static bool IsUniqueViolation(
        Exception exception)
    {
        Exception? currentException =
            exception;

        while (currentException is not null)
        {
            if (
                currentException
                    is PostgresException postgresException &&
                postgresException.SqlState ==
                    PostgresErrorCodes.UniqueViolation
            )
            {
                return true;
            }

            currentException =
                currentException.InnerException;
        }

        return false;
    }
}