using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Wallets;
using TrustFlow.Api.Services;
using TrustFlow.Api.Validation;
using Nethereum.Signer;
using Npgsql;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/wallet")]
[Authorize]
public sealed class WalletController(
    AppDbContext dbContext,
    IHostEnvironment hostEnvironment,
    IOptions<BlockchainOptions> blockchainOptions)
    : ControllerBase
{
    private static readonly HttpClient
        LocalChainHttpClient = new();

    private const long LocalHardhatChainId = 31_337;

    private const string LocalFaucetGasBalanceHex =
        "0x8ac7230489e80000";

    [HttpGet("me")]
    public async Task<IActionResult> GetMyWallet(
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

        var wallet =
            await dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id == userId)
                .Select(user => new
                {
                    user.WalletAddress,
                    user.WalletVerifiedAt
                })
                .FirstOrDefaultAsync(
                    cancellationToken
                );

        if (wallet is null)
        {
            return Unauthorized(new
            {
                message =
                    "User account could not be found."
            });
        }

        var isVerified =
            !string.IsNullOrWhiteSpace(
                wallet.WalletAddress
            ) &&
            wallet.WalletVerifiedAt.HasValue;

        return Ok(new WalletStatusResponse
        {
            WalletAddress = isVerified
                ? wallet.WalletAddress
                : null,

            VerifiedAt = isVerified
                ? wallet.WalletVerifiedAt
                : null,

            IsVerified = isVerified
        });
    }

    [HttpPost("local-test-gas")]
    public async Task<IActionResult> AddLocalTestGas(
        CancellationToken cancellationToken)
    {
        var options = blockchainOptions.Value;

        if (
            !hostEnvironment.IsDevelopment() ||
            options.ChainId != LocalHardhatChainId
        )
        {
            return NotFound();
        }

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

        var wallet =
            await dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id == userId)
                .Select(user => new
                {
                    user.WalletAddress,
                    user.WalletVerifiedAt
                })
                .FirstOrDefaultAsync(
                    cancellationToken
                );

        if (
            wallet is null ||
            string.IsNullOrWhiteSpace(
                wallet.WalletAddress
            ) ||
            !wallet.WalletVerifiedAt.HasValue
        )
        {
            return Conflict(new
            {
                message =
                    "Connect and verify your wallet before requesting local test gas."
            });
        }

        using var rpcRequest =
            JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method =
                    "hardhat_setBalance",
                @params = new[]
                {
                    wallet.WalletAddress,
                    LocalFaucetGasBalanceHex
                }
            });

        HttpResponseMessage rpcResponse;

        try
        {
            rpcResponse =
                await LocalChainHttpClient.PostAsync(
                    options.RpcUrl,
                    rpcRequest,
                    cancellationToken
                );
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    message =
                        "The local Hardhat node is unavailable."
                }
            );
        }

        using (rpcResponse)
        {
            if (!rpcResponse.IsSuccessStatusCode)
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message =
                            "The local Hardhat node rejected the gas request."
                    }
                );
            }

            await using var responseStream =
                await rpcResponse.Content
                    .ReadAsStreamAsync(
                        cancellationToken
                    );

            using var responseDocument =
                await JsonDocument.ParseAsync(
                    responseStream,
                    cancellationToken:
                        cancellationToken
                );

            if (
                responseDocument.RootElement
                    .TryGetProperty(
                        "error",
                        out _
                    )
            )
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    new
                    {
                        message =
                            "The local Hardhat node could not add test gas."
                    }
                );
            }
        }

        return Ok(new
        {
            walletAddress =
                wallet.WalletAddress,
            balanceWei =
                "10000000000000000000"
        });
    }

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

        if (
            !string.IsNullOrWhiteSpace(
                user.WalletAddressNormalized
            ) &&
            user.WalletVerifiedAt.HasValue
        )
        {
            return Conflict(new
            {
                message =
                    "A different wallet is already verified for your account. Wallet replacement requires a dedicated recovery flow."
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
