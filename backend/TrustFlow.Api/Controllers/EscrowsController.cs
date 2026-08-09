using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/escrow")]
public sealed class EscrowsController(
    AppDbContext dbContext)
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

        var escrow = new Escrow
        {
            ProjectId = project.Id,
            ChainId = request.ChainId,
            TokenAddress =
                tokenAddress.ToLowerInvariant(),
            TotalAmount = project.Budget,
            ReleasedAmount = 0m,
            Status =
                EscrowStatus.PendingDeployment,
            CreatedAt =
                DateTimeOffset.UtcNow,
            UpdatedAt =
                DateTimeOffset.UtcNow
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
            CreatedAt = escrow.CreatedAt,
            UpdatedAt = escrow.UpdatedAt
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