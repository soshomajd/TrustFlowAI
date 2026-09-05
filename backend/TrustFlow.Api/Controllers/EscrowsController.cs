using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/escrow")]
public sealed class EscrowsController(
    AppDbContext dbContext,
    IEscrowDeploymentOrchestrator deploymentOrchestrator,
    IEscrowFundingSyncOrchestrator fundingSyncOrchestrator)
    : ControllerBase
{
    [Authorize(Roles = AppRoles.Client)]
    [HttpPost]
    public async Task<IActionResult> CreateEscrow(
        Guid projectId,
        CreateEscrowRequest request,
        CancellationToken cancellationToken)
    {
        var clientIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (!Guid.TryParse(
            clientIdValue,
            out var clientId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user identity."
            });
        }

        if (request.ChainId <= 0)
        {
            return BadRequest(new
            {
                message =
                    "Chain id must be greater than zero."
            });
        }

        var tokenAddress =
            request.TokenAddress.Trim();

        if (!IsBlockchainAddress(
            tokenAddress))
        {
            return BadRequest(new
            {
                message =
                    "Token address is invalid."
            });
        }

        var project =
            await dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    project =>
                        project.Id ==
                        projectId &&
                        project.ClientId ==
                        clientId,
                    cancellationToken
                );

        if (project is null)
        {
            return NotFound(new
            {
                message =
                    "Project not found."
            });
        }

        if (project.Status !=
            ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Escrow can only be created for an in-progress project.",
                currentProjectStatus =
                    project.Status
            });
        }

        if (!project.FreelancerId.HasValue)
        {
            return Conflict(new
            {
                message =
                    "The project does not have an assigned freelancer."
            });
        }
        var clientWallet =
    await dbContext.Users
        .AsNoTracking()
        .Where(user =>
            user.Id == clientId)
        .Select(user => new
        {
            user.WalletAddress,
            user.WalletAddressNormalized,
            user.WalletVerifiedAt
        })
        .FirstOrDefaultAsync(
            cancellationToken
        );

        if (
            clientWallet is null ||
            string.IsNullOrWhiteSpace(
                clientWallet.WalletAddress
            ) ||
            string.IsNullOrWhiteSpace(
                clientWallet.WalletAddressNormalized
            ) ||
            !clientWallet.WalletVerifiedAt.HasValue
        )
        {
            return Conflict(new
            {
                message =
                    "You must connect and verify your wallet before creating an escrow."
            });
        }

        var freelancerWallet =
            await dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    user.Id ==
                    project.FreelancerId.Value)
                .Select(user => new
                {
                    user.WalletAddress,
                    user.WalletAddressNormalized,
                    user.WalletVerifiedAt
                })
                .FirstOrDefaultAsync(
                    cancellationToken
                );

        if (
            freelancerWallet is null ||
            string.IsNullOrWhiteSpace(
                freelancerWallet.WalletAddress
            ) ||
            string.IsNullOrWhiteSpace(
                freelancerWallet.WalletAddressNormalized
            ) ||
            !freelancerWallet.WalletVerifiedAt.HasValue
        )
        {
            return Conflict(new
            {
                message =
                    "The assigned freelancer must connect and verify a wallet before an escrow can be created."
            });
        }

        var escrowExists =
            await dbContext.Escrows
                .AsNoTracking()
                .AnyAsync(
                    escrow =>
                        escrow.ProjectId ==
                        projectId,
                    cancellationToken
                );

        if (escrowExists)
        {
            return Conflict(new
            {
                message =
                    "An escrow already exists for this project."
            });
        }

        var now =
            DateTimeOffset.UtcNow;

        var escrow = new Escrow
        {
            ProjectId = project.Id,

            ChainId = request.ChainId,

            TokenAddress =
                tokenAddress.ToLowerInvariant(),

            ClientWalletAddress =
                clientWallet.WalletAddress,

            FreelancerWalletAddress =
                freelancerWallet.WalletAddress,

            TotalAmount =
                project.Budget,

            ReleasedAmount = 0m,

            Status =
                EscrowStatus.PendingDeployment,

            CreatedAt = now,

            UpdatedAt = now
        };

        dbContext.Escrows.Add(escrow);

        try
        {
            await dbContext.SaveChangesAsync(
                cancellationToken
            );
        }
        catch (DbUpdateException exception)
            when (IsUniqueViolation(
                exception))
        {
            return Conflict(new
            {
                message =
                    "An escrow already exists for this project."
            });
        }

        return Created(
            $"/api/projects/{projectId}/escrow",
            ToResponse(escrow)
        );
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetEscrow(
    Guid projectId,
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

        var escrow =
            await dbContext.Escrows
                .AsNoTracking()
                .Where(item =>
                    item.ProjectId == projectId &&
                    (
                        item.Project.ClientId ==
                        userId ||
                        item.Project.FreelancerId ==
                        userId
                    ))
                .FirstOrDefaultAsync(
                    cancellationToken
                );

        if (escrow is null)
        {
            return NotFound(new
            {
                message =
                    "Escrow not found."
            });
        }

        return Ok(ToResponse(escrow));
    }

    [Authorize(Roles = AppRoles.Client)]
    [HttpPost("deploy")]
    public async Task<IActionResult> DeployEscrow(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var clientIdValue =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

        if (!Guid.TryParse(
            clientIdValue,
            out var clientId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user identity."
            });
        }

        var escrow =
            await dbContext.Escrows
                .FirstOrDefaultAsync(
                    item =>
                        item.ProjectId == projectId &&
                        item.Project.ClientId == clientId,
                    cancellationToken
                );

        if (escrow is null)
        {
            return NotFound(new
            {
                message =
                    "Escrow not found."
            });
        }

        var deployed =
            await deploymentOrchestrator.TryDeployAsync(
                escrow.Id,
                cancellationToken
            );

        if (!deployed)
        {
            var currentStatus =
                await dbContext.Escrows
                    .AsNoTracking()
                    .Where(item => item.Id == escrow.Id)
                    .Select(item => item.Status)
                    .FirstAsync(cancellationToken);

            return Conflict(new
            {
                message =
                    "Escrow is not pending deployment.",
                currentEscrowStatus =
                    currentStatus
            });
        }

        var deployedEscrow =
            await dbContext.Escrows
                .AsNoTracking()
                .FirstAsync(
                    item => item.Id == escrow.Id,
                    cancellationToken
                );

        return Ok(ToResponse(deployedEscrow));
    }

    [Authorize]
    [HttpPost("sync")]
    public async Task<IActionResult> SyncEscrow(
        Guid projectId,
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

        var escrow =
            await dbContext.Escrows
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item =>
                        item.ProjectId == projectId &&
                        (
                            item.Project.ClientId ==
                            userId ||
                            item.Project.FreelancerId ==
                            userId
                        ),
                    cancellationToken
                );

        if (escrow is null)
        {
            return NotFound(new
            {
                message =
                    "Escrow not found."
            });
        }

        await fundingSyncOrchestrator.TrySyncFundingAsync(
            escrow.Id,
            cancellationToken
        );

        var currentEscrow =
            await dbContext.Escrows
                .AsNoTracking()
                .FirstAsync(
                    item => item.Id == escrow.Id,
                    cancellationToken
                );

        return Ok(ToResponse(currentEscrow));
    }

    private static EscrowResponse ToResponse(
        Escrow escrow)
    {
        return new EscrowResponse
        {
            Id = escrow.Id,
            ProjectId = escrow.ProjectId,
            ChainId = escrow.ChainId,
            TokenAddress =
                escrow.TokenAddress,
            ContractAddress =
                escrow.ContractAddress,
            ClientWalletAddress =
                escrow.ClientWalletAddress,
            FreelancerWalletAddress =
                escrow.FreelancerWalletAddress,
            TotalAmount =
                escrow.TotalAmount,
            ReleasedAmount =
                escrow.ReleasedAmount,
            Status = escrow.Status,
            DeploymentTransactionHash =
                escrow.DeploymentTransactionHash,
            FundingTransactionHash =
                escrow.FundingTransactionHash,
            CancellationTransactionHash =
                escrow.CancellationTransactionHash,
            CreatedAt = escrow.CreatedAt,
            UpdatedAt = escrow.UpdatedAt,
            DeployedAt = escrow.DeployedAt,
            FundedAt = escrow.FundedAt,
            CompletedAt = escrow.CompletedAt,
            CancelledAt = escrow.CancelledAt
        };
    }

    private static bool IsBlockchainAddress(
        string value)
    {
        if (value.Length != 42 ||
            !value.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value
            .AsSpan(2)
            .ToString()
            .All(Uri.IsHexDigit);
    }

    private static bool IsUniqueViolation(
        Exception exception)
    {
        Exception? currentException =
            exception;

        while (currentException is not null)
        {
            if (currentException
                    is PostgresException
                    postgresException &&
                postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }

            currentException =
                currentException.InnerException;
        }

        return false;
    }
}