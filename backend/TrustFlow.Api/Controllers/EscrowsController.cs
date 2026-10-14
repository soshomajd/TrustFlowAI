using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/escrow")]
public sealed class EscrowsController(
    AppDbContext dbContext,
    IEscrowDeploymentOrchestrator deploymentOrchestrator,
    IEscrowChainSyncOrchestrator chainSyncOrchestrator,
    IOptions<BlockchainOptions> blockchainOptions)
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

        var configuredBlockchain = blockchainOptions.Value;

        if (request.ChainId != configuredBlockchain.ChainId)
        {
            return BadRequest(new
            {
                message =
                    $"Escrows must use the configured chain " +
                    $"{configuredBlockchain.ChainId}.",
                configuredChainId =
                    configuredBlockchain.ChainId
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

        if (!string.Equals(
                tokenAddress,
                configuredBlockchain.TokenAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Escrows must use the configured payment token.",
                configuredTokenAddress =
                    configuredBlockchain.TokenAddress
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

        // One escrow per freelancer. The request names which of the project's
        // freelancers this one pays, and may leave that out only while a
        // single freelancer holds every assigned milestone.
        var assignments = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.ProjectId == projectId &&
                milestone.FreelancerId != null &&
                (request.FreelancerId == null ||
                 milestone.FreelancerId ==
                    request.FreelancerId))
            .GroupBy(milestone =>
                milestone.FreelancerId!.Value)
            .Select(group => new
            {
                FreelancerId = group.Key,
                Total = group.Sum(
                    milestone => milestone.Amount
                )
            })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return Conflict(new
            {
                message = request.FreelancerId == null
                    ? "No freelancer has been assigned milestones on this project yet."
                    : "That freelancer has no milestones on this project."
            });
        }

        if (assignments.Count > 1)
        {
            return BadRequest(new
            {
                message =
                    "This project is split between several freelancers. " +
                    "Name the freelancer this escrow pays with " +
                    "freelancerId."
            });
        }

        var assignment = assignments[0];
        var freelancerId = assignment.FreelancerId;
        var assignedTotal = assignment.Total;
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
                    user.Id == freelancerId)
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
                        escrow.ProjectId == projectId &&
                        escrow.FreelancerId ==
                            freelancerId,
                    cancellationToken
                );

        if (escrowExists)
        {
            return Conflict(new
            {
                message =
                    "An escrow already exists for this freelancer on this project."
            });
        }

        var feeRecipient = blockchainOptions.Value.FeeRecipientAddress;
        if (!IsBlockchainAddress(feeRecipient) || feeRecipient.Equals("0x" + new string('0', 40), StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(503, new { code = "fee_recipient_not_configured", message = "Escrow setup is unavailable until the platform fee receiving wallet is configured." });
        }

        var now =
            DateTimeOffset.UtcNow;

        var escrow = new Escrow
        {
            ProjectId = project.Id,

            FreelancerId = freelancerId,
            PlatformFeeBasisPoints = 1000,
            FeeRecipientAddress = feeRecipient.ToLowerInvariant(),

            ChainId = request.ChainId,

            TokenAddress =
                tokenAddress.ToLowerInvariant(),

            ClientWalletAddress =
                clientWallet.WalletAddress,

            FreelancerWalletAddress =
                freelancerWallet.WalletAddress,

            // Only this freelancer's share, not the whole budget.
            TotalAmount = assignedTotal,

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

    // Current assignments plus persisted escrows, including departed
    // participants. The client sees all; a freelancer sees only their own.
    [Authorize]
    [HttpGet("~/api/projects/{projectId:guid}/escrows")]
    public async Task<IActionResult> GetProjectEscrows(
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

        var isClient =
            await dbContext.Projects
                .AsNoTracking()
                .AnyAsync(
                    project =>
                        project.Id == projectId &&
                        project.ClientId == userId,
                    cancellationToken
                );

        var assignments =
            await dbContext.Milestones
                .AsNoTracking()
                .Where(milestone =>
                    milestone.ProjectId == projectId &&
                    milestone.FreelancerId != null &&
                    (isClient ||
                     milestone.FreelancerId == userId))
                .GroupBy(milestone =>
                    milestone.FreelancerId!.Value)
                .Select(group => new
                {
                    FreelancerId = group.Key,
                    MilestoneCount = group.Count(),
                    AssignedTotal = group.Sum(
                        milestone => milestone.Amount
                    )
                })
                .ToListAsync(cancellationToken);

        var escrows = await dbContext.Escrows
            .AsNoTracking()
            .Where(escrow =>
                escrow.ProjectId == projectId &&
                (isClient || escrow.FreelancerId == userId))
            .ToListAsync(cancellationToken);

        if (!isClient && assignments.Count == 0 && escrows.Count == 0)
        {
            return NotFound(new
            {
                message =
                    "Project not found."
            });
        }

        var freelancerIds = assignments
            .Select(assignment => assignment.FreelancerId)
            .Concat(escrows.Select(escrow => escrow.FreelancerId))
            .Distinct()
            .ToList();

        var freelancerNames =
            await dbContext.Users
                .AsNoTracking()
                .Where(user =>
                    freelancerIds.Contains(user.Id))
                .Select(user => new
                {
                    user.Id,
                    user.FullName
                })
                .ToDictionaryAsync(
                    user => user.Id,
                    user => user.FullName,
                    cancellationToken
                );

        var participants = freelancerIds
            .Select(freelancerId =>
                new ProjectEscrowParticipantResponse
                {
                    FreelancerId =
                        freelancerId,

                    FreelancerFullName =
                        freelancerNames.GetValueOrDefault(
                            freelancerId,
                            string.Empty
                        ),

                    MilestoneCount =
                        assignments.FirstOrDefault(item => item.FreelancerId == freelancerId)?.MilestoneCount ?? 0,

                    AssignedTotal =
                        assignments.FirstOrDefault(item => item.FreelancerId == freelancerId)?.AssignedTotal ?? 0m,

                    Escrow = escrows
                        .Where(escrow =>
                            escrow.FreelancerId ==
                            freelancerId)
                        .Select(ToResponse)
                        .FirstOrDefault()
                })
            .OrderBy(
                participant =>
                    participant.FreelancerFullName,
                StringComparer.OrdinalIgnoreCase
            )
            .ToList();

        return Ok(participants);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetEscrow(
    Guid projectId,
    [FromQuery] Guid? freelancerId,
    CancellationToken cancellationToken)
    {
        var resolved = await ResolveEscrowAsync(
            projectId,
            freelancerId,
            cancellationToken
        );

        if (resolved.Error is not null)
        {
            return resolved.Error;
        }

        return Ok(ToResponse(resolved.Escrow!));
    }

    [Authorize(Roles = AppRoles.Client)]
    [HttpPost("deploy")]
    public async Task<IActionResult> DeployEscrow(
        Guid projectId,
        [FromQuery] Guid? freelancerId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveEscrowAsync(
            projectId,
            freelancerId,
            cancellationToken
        );

        if (resolved.Error is not null)
        {
            return resolved.Error;
        }

        var escrow = resolved.Escrow!;

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
        [FromQuery] Guid? freelancerId,
        CancellationToken cancellationToken)
    {
        var resolved = await ResolveEscrowAsync(
            projectId,
            freelancerId,
            cancellationToken
        );

        if (resolved.Error is not null)
        {
            return resolved.Error;
        }

        var escrow = resolved.Escrow!;

        await chainSyncOrchestrator.TrySyncAsync(
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

    // A project can hold several escrows, so callers name the freelancer they
    // mean. The name may be omitted only while the project has a single
    // escrow, and a freelancer always resolves to their own.
    private async Task<(Escrow? Escrow, IActionResult? Error)>
        ResolveEscrowAsync(
            Guid projectId,
            Guid? freelancerId,
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
            return (null, Unauthorized(new
            {
                message =
                    "Invalid user identity."
            }));
        }

        var isClient =
            await dbContext.Projects
                .AsNoTracking()
                .AnyAsync(
                    project =>
                        project.Id == projectId &&
                        project.ClientId == userId,
                    cancellationToken
                );

        // A freelancer can only ever reach their own escrow, so their own id
        // overrides whatever the query string asked for.
        var targetFreelancerId = isClient
            ? freelancerId
            : userId;

        var candidates =
            await dbContext.Escrows
                .AsNoTracking()
                .Where(item =>
                    item.ProjectId == projectId &&
                    (isClient ||
                     item.FreelancerId == userId) &&
                    (targetFreelancerId == null ||
                     item.FreelancerId ==
                        targetFreelancerId))
                .OrderBy(item => item.CreatedAt)
                .Take(2)
                .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return (null, NotFound(new
            {
                message =
                    "Escrow not found."
            }));
        }

        if (candidates.Count > 1)
        {
            return (null, BadRequest(new
            {
                message =
                    "This project has an escrow for each assigned " +
                    "freelancer. Name the freelancer you mean with " +
                    "the freelancerId query parameter."
            }));
        }

        return (candidates[0], null);
    }

    private static EscrowResponse ToResponse(
        Escrow escrow)
    {
        return new EscrowResponse
        {
            Id = escrow.Id,
            ProjectId = escrow.ProjectId,
            FreelancerId = escrow.FreelancerId,
            ChainId = escrow.ChainId,
            TokenAddress =
                escrow.TokenAddress,
            ContractAddress =
                escrow.ContractAddress,
            ClientWalletAddress =
                escrow.ClientWalletAddress,
            FreelancerWalletAddress =
                escrow.FreelancerWalletAddress,
            ArbiterWalletAddress =
                escrow.ArbiterWalletAddress,
            TotalAmount =
                escrow.TotalAmount,
            ReleasedAmount =
                escrow.ReleasedAmount,
            PlatformFeeBasisPoints = escrow.PlatformFeeBasisPoints,
            TokenDecimals = escrow.TokenDecimals,
            FeeRecipientAddress = escrow.FeeRecipientAddress,
            PlatformFeesPaid = escrow.PlatformFeesPaid,
            FreelancerNetPaid = escrow.ReleasedAmount - escrow.PlatformFeesPaid,
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
