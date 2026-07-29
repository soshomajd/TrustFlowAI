using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Common;
using TrustFlow.Api.Dtos.Proposals;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/proposals")]
public class ProposalsController(AppDbContext dbContext)
    : ControllerBase
{
    private static bool IsUniqueViolation(Exception exception)
    {
        Exception? currentException = exception;

        while (currentException is not null)
        {
            if (currentException is PostgresException postgresException &&
                postgresException.SqlState ==
                PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }

            currentException = currentException.InnerException;
        }

        return false;
    }

    private static bool IsSerializationFailure(
    Exception exception)
    {
        Exception? currentException = exception;

        while (currentException is not null)
        {
            if (currentException
                    is PostgresException postgresException &&
                postgresException.SqlState ==
                PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }

            currentException =
                currentException.InnerException;
        }

        return false;
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPost]
    public async Task<IActionResult> CreateProposal(
     Guid projectId,
     CreateProposalRequest request,
     CancellationToken cancellationToken)
    {
        var freelancerIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
            freelancerIdValue,
            out var freelancerId))
        {
            return Unauthorized();
        }

        const int maxAttempts = 3;

        for (var attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.Serializable,
                        cancellationToken
                    );

                var project = await dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        project =>
                            project.Id == projectId,
                        cancellationToken
                    );

                if (project is null)
                {
                    return NotFound(new
                    {
                        message = "Project not found."
                    });
                }

                if (project.Status != ProjectStatus.Open)
                {
                    return Conflict(new
                    {
                        message =
                            "Proposals can only be submitted for open projects.",
                        currentProjectStatus = project.Status
                    });
                }

                var proposalExists = await dbContext.Proposals
                    .AsNoTracking()
                    .AnyAsync(
                        proposal =>
                            proposal.ProjectId == projectId &&
                            proposal.FreelancerId ==
                                freelancerId,
                        cancellationToken
                    );

                if (proposalExists)
                {
                    return Conflict(new
                    {
                        message =
                            "You have already submitted a proposal for this project."
                    });
                }

                var proposal = new Proposal
                {
                    ProjectId = projectId,
                    FreelancerId = freelancerId,
                    CoverLetter =
                        request.CoverLetter.Trim(),
                    BidAmount = request.BidAmount,
                    EstimatedDays =
                        request.EstimatedDays,
                    Status = ProposalStatus.Pending,
                    CreatedAt =
                        DateTimeOffset.UtcNow
                };

                dbContext.Proposals.Add(proposal);

                await dbContext.SaveChangesAsync(
                    cancellationToken
                );

                await transaction.CommitAsync(
                    cancellationToken
                );

                var response = new ProposalResponse
                {
                    Id = proposal.Id,
                    ProjectId = proposal.ProjectId,
                    FreelancerId =
                        proposal.FreelancerId,
                    CoverLetter =
                        proposal.CoverLetter,
                    BidAmount = proposal.BidAmount,
                    EstimatedDays =
                        proposal.EstimatedDays,
                    Status = proposal.Status,
                    CreatedAt = proposal.CreatedAt
                };

                return Created(
                    $"/api/projects/{projectId}/proposals/{proposal.Id}",
                    response
                );
            }
            catch (Exception exception)
                when (
                    IsSerializationFailure(exception) &&
                    attempt < maxAttempts
                )
            {
                dbContext.ChangeTracker.Clear();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        50 * attempt
                    ),
                    cancellationToken
                );
            }
            catch (Exception exception)
                when (IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message =
                        "The project was changed by another request. Please try again."
                });
            }
            catch (Exception exception)
                when (IsUniqueViolation(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message =
                        "You have already submitted a proposal for this project."
                });
            }
        }

        return Conflict(new
        {
            message =
                "The proposal could not be submitted due to concurrent requests."
        });
    }
    [Authorize(Roles = AppRoles.Client)]
    [HttpGet]
    public async Task<IActionResult> GetProjectProposals(
     Guid projectId,
     [FromQuery] PaginationRequest pagination,
     CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.Id == projectId &&
                    project.ClientId == clientId,
                cancellationToken
            );

        if (!projectExists)
        {
            return NotFound(new
            {
                message = "Project not found."
            });
        }

        var query = dbContext.Proposals
            .AsNoTracking()
            .Where(proposal =>
                proposal.ProjectId == projectId);

        var totalItems = await query.CountAsync(
            cancellationToken
        );

        var items = await query
            .OrderByDescending(proposal =>
                proposal.CreatedAt)
            .Skip(
                (pagination.Page - 1) *
                pagination.PageSize
            )
            .Take(pagination.PageSize)
            .Select(proposal =>
                new ProposalForClientResponse
                {
                    Id = proposal.Id,
                    ProjectId = proposal.ProjectId,
                    FreelancerId = proposal.FreelancerId,

                    FreelancerFullName =
                        proposal.Freelancer.FullName,

                    CoverLetter = proposal.CoverLetter,
                    BidAmount = proposal.BidAmount,
                    EstimatedDays = proposal.EstimatedDays,
                    Status = proposal.Status,
                    CreatedAt = proposal.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalItems /
            (double)pagination.PageSize
        );

        var response =
            new PagedResponse<ProposalForClientResponse>
            {
                Items = items,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,

                HasPreviousPage =
                    pagination.Page > 1,

                HasNextPage =
                    pagination.Page < totalPages
            };

        return Ok(response);
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpGet("~/api/proposals/mine")]
    public async Task<IActionResult> GetMyProposals(
      [FromQuery] PaginationRequest pagination,
      CancellationToken cancellationToken)
    {
        var freelancerIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
            freelancerIdValue,
            out var freelancerId))
        {
            return Unauthorized();
        }

        var query = dbContext.Proposals
            .AsNoTracking()
            .Where(proposal =>
                proposal.FreelancerId == freelancerId);

        var totalItems = await query.CountAsync(
            cancellationToken
        );

        var items = await query
            .OrderByDescending(proposal =>
                proposal.CreatedAt)
            .Skip(
                (pagination.Page - 1) *
                pagination.PageSize
            )
            .Take(pagination.PageSize)
            .Select(proposal =>
                new MyProposalResponse
                {
                    Id = proposal.Id,
                    ProjectId = proposal.ProjectId,

                    ProjectTitle =
                        proposal.Project.Title,

                    CoverLetter = proposal.CoverLetter,
                    BidAmount = proposal.BidAmount,
                    EstimatedDays = proposal.EstimatedDays,
                    Status = proposal.Status,
                    CreatedAt = proposal.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(
            totalItems /
            (double)pagination.PageSize
        );

        var response =
            new PagedResponse<MyProposalResponse>
            {
                Items = items,
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,

                HasPreviousPage =
                    pagination.Page > 1,

                HasNextPage =
                    pagination.Page < totalPages
            };

        return Ok(response);
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPatch("~/api/proposals/{proposalId:guid}/withdraw")]
    public async Task<IActionResult> WithdrawProposal(
    Guid proposalId,
    CancellationToken cancellationToken)
    {
        var freelancerIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
            freelancerIdValue,
            out var freelancerId))
        {
            return Unauthorized();
        }

        var affectedRows = await dbContext.Proposals
            .Where(proposal =>
                proposal.Id == proposalId &&
                proposal.FreelancerId == freelancerId &&
                proposal.Status == ProposalStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    proposal => proposal.Status,
                    ProposalStatus.Withdrawn
                ),
                cancellationToken
            );

        if (affectedRows == 1)
        {
            return Ok(new ProposalStatusResponse
            {
                Id = proposalId,
                Status = ProposalStatus.Withdrawn
            });
        }

        var proposalStatus = await dbContext.Proposals
            .AsNoTracking()
            .Where(proposal =>
                proposal.Id == proposalId &&
                proposal.FreelancerId == freelancerId)
            .Select(proposal => (ProposalStatus?)proposal.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (proposalStatus is null)
        {
            return NotFound(new
            {
                message = "Proposal not found."
            });
        }

        return Conflict(new
        {
            message =
                "Only a pending proposal can be withdrawn.",
            currentStatus = proposalStatus.Value
        });
    }
    [Authorize(Roles = AppRoles.Client)]
    [HttpPatch("{proposalId:guid}/reject")]
    public async Task<IActionResult> RejectProposal(
    Guid projectId,
    Guid proposalId,
    CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var projectExists = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.Id == projectId &&
                    project.ClientId == clientId,
                cancellationToken
            );

        if (!projectExists)
        {
            return NotFound(new
            {
                message = "Project not found."
            });
        }

        var affectedRows = await dbContext.Proposals
            .Where(proposal =>
                proposal.Id == proposalId &&
                proposal.ProjectId == projectId &&
                proposal.Status == ProposalStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    proposal => proposal.Status,
                    ProposalStatus.Rejected
                ),
                cancellationToken
            );

        if (affectedRows == 1)
        {
            return Ok(new ProposalStatusResponse
            {
                Id = proposalId,
                Status = ProposalStatus.Rejected
            });
        }

        var proposalStatus = await dbContext.Proposals
            .AsNoTracking()
            .Where(proposal =>
                proposal.Id == proposalId &&
                proposal.ProjectId == projectId)
            .Select(proposal =>
                (ProposalStatus?)proposal.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (proposalStatus is null)
        {
            return NotFound(new
            {
                message = "Proposal not found."
            });
        }

        return Conflict(new
        {
            message =
                "Only a pending proposal can be rejected.",
            currentStatus = proposalStatus.Value
        });
    }

    [Authorize(Roles = AppRoles.Client)]
    [HttpPatch("{proposalId:guid}/accept")]
    public async Task<IActionResult> AcceptProposal(
    Guid projectId,
    Guid proposalId,
    CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        const int maxAttempts = 3;

        for (var attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            try
            {
                await using var transaction =
                    await dbContext.Database
                        .BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable,
                            cancellationToken
                        );

                var project = await dbContext.Projects
                    .FirstOrDefaultAsync(
                        project =>
                            project.Id == projectId &&
                            project.ClientId == clientId,
                        cancellationToken
                    );

                if (project is null)
                {
                    return NotFound(new
                    {
                        message = "Project not found."
                    });
                }

                if (project.Status != ProjectStatus.Open)
                {
                    return Conflict(new
                    {
                        message =
                            "This project already has an assigned freelancer.",
                        currentStatus = project.Status
                    });
                }

                var proposal = await dbContext.Proposals
                    .FirstOrDefaultAsync(
                        proposal =>
                            proposal.Id == proposalId &&
                            proposal.ProjectId == projectId,
                        cancellationToken
                    );

                if (proposal is null)
                {
                    return NotFound(new
                    {
                        message = "Proposal not found."
                    });
                }

                if (proposal.Status != ProposalStatus.Pending)
                {
                    return Conflict(new
                    {
                        message =
                            "Only a pending proposal can be accepted.",
                        currentStatus = proposal.Status
                    });
                }
                proposal.Status = ProposalStatus.Accepted;
                project.FreelancerId = proposal.FreelancerId;
                project.Status = ProjectStatus.InProgress;

                await dbContext.Proposals
                    .Where(otherProposal =>
                        otherProposal.ProjectId == projectId &&
                        otherProposal.Id != proposalId &&
                        otherProposal.Status ==
                            ProposalStatus.Pending)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            otherProposal =>
                                otherProposal.Status,
                            ProposalStatus.Rejected
                        ),
                        cancellationToken
                    );

                await dbContext.SaveChangesAsync(
                    cancellationToken
                );

                await transaction.CommitAsync(
                    cancellationToken
                );

                return Ok(new AcceptProposalResponse
                {
                    ProposalId = proposal.Id,
                    ProjectId = project.Id,
                    FreelancerId =
                        proposal.FreelancerId,
                    ProposalStatus =
                        ProposalStatus.Accepted,
                    ProjectStatus =
                        ProjectStatus.InProgress
                });
            }
            catch (Exception exception)
                when (
                    IsSerializationFailure(exception) &&
                    attempt < maxAttempts
                )
            {
                dbContext.ChangeTracker.Clear();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(
                        50 * attempt
                    ),
                    cancellationToken
                );
            }
            catch (Exception exception)
                when (IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message =
                        "Another request changed this project. Please try again."
                });
            }
        }

        return Conflict(new
        {
            message =
                "The proposal could not be accepted due to concurrent requests."
        });
    }


}