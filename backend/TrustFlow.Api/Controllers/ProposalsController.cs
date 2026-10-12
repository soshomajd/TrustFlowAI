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
    .Where(project =>
        project.Id == projectId
    )
    .Select(project => new
    {
        project.Status,
        project.Budget,

        MilestoneCount =
            project.Milestones.Count,

        AllocatedAmount =
            project.Milestones
                .Sum(milestone =>
                    (decimal?)milestone.Amount
                ) ?? 0m
    })
    .FirstOrDefaultAsync(
        cancellationToken
    );

                if (project is null)
                {
                    return NotFound(new
                    {
                        message = "Project not found."
                    });
                }

                // A project goes InProgress as soon as its first freelancer
                // is accepted, but the stages nobody has taken are still
                // hiring, so it keeps accepting proposals for them.
                if (project.Status != ProjectStatus.Open &&
                    project.Status != ProjectStatus.InProgress)
                {
                    return Conflict(new
                    {
                        message =
                            "Proposals can only be submitted for open projects.",
                        currentProjectStatus = project.Status
                    });
                }
                if (project.MilestoneCount == 0)
                {
                    return Conflict(new
                    {
                        message =
                            "This project is not ready for proposals because it has no milestones."
                    });
                }

                if (project.AllocatedAmount != project.Budget)
                {
                    return Conflict(new
                    {
                        message =
                            "This project is not ready for proposals because its milestone amounts do not match the project budget.",

                        projectBudget =
                            project.Budget,

                        allocatedAmount =
                            project.AllocatedAmount,

                        remainingAmount =
                            project.Budget -
                            project.AllocatedAmount
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

                var requestedMilestoneIds = request.MilestoneIds
                    .Distinct()
                    .ToList();

                var chosenMilestones = await dbContext.Milestones
                    .AsNoTracking()
                    .Where(milestone =>
                        milestone.ProjectId == projectId &&
                        requestedMilestoneIds.Contains(milestone.Id))
                    .Select(milestone => new
                    {
                        milestone.Id,
                        milestone.Title,
                        milestone.Amount,
                        milestone.SequenceNumber,
                        milestone.FreelancerId
                    })
                    .ToListAsync(cancellationToken);

                if (chosenMilestones.Count !=
                    requestedMilestoneIds.Count)
                {
                    return BadRequest(new
                    {
                        message =
                            "One or more of the chosen milestones do not belong to this project."
                    });
                }

                // A milestone already promised to someone else is not
                // available to bid for.
                if (chosenMilestones.Any(milestone =>
                        milestone.FreelancerId.HasValue))
                {
                    return Conflict(new
                    {
                        message =
                            "One or more of the chosen milestones are already assigned to another freelancer."
                    });
                }

                var proposal = new Proposal
                {
                    ProjectId = projectId,
                    FreelancerId = freelancerId,
                    CoverLetter =
                        request.CoverLetter.Trim(),
                    // Fixed price set by the client: this records what the
                    // chosen milestones pay, not an offer of the
                    // freelancer's own.
                    BidAmount = chosenMilestones
                        .Sum(milestone => milestone.Amount),
                    EstimatedDays =
                        request.EstimatedDays,
                    Status = ProposalStatus.Pending,
                    CreatedAt =
                        DateTimeOffset.UtcNow
                };

                dbContext.Proposals.Add(proposal);

                foreach (var milestone in chosenMilestones)
                {
                    dbContext.ProposalMilestones.Add(
                        new ProposalMilestone
                        {
                            ProposalId = proposal.Id,
                            MilestoneId = milestone.Id
                        }
                    );
                }

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
                    CreatedAt = proposal.CreatedAt,

                    Milestones = chosenMilestones
                        .OrderBy(milestone =>
                            milestone.SequenceNumber)
                        .Select(milestone =>
                            new ProposalMilestoneResponse
                            {
                                Id = milestone.Id,
                                Title = milestone.Title,
                                Amount = milestone.Amount,
                                SequenceNumber =
                                    milestone.SequenceNumber
                            })
                        .ToList()
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
                    CreatedAt = proposal.CreatedAt,

                    Milestones = proposal.ProposalMilestones
                        .OrderBy(link => link.Milestone.SequenceNumber)
                        .Select(link =>
                            new ProposalMilestoneResponse
                            {
                                Id = link.MilestoneId,
                                Title = link.Milestone.Title,
                                Amount = link.Milestone.Amount,
                                SequenceNumber =
                                    link.Milestone.SequenceNumber
                            })
                        .ToList()
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
                    CreatedAt = proposal.CreatedAt,

                    Milestones = proposal.ProposalMilestones
                        .OrderBy(link => link.Milestone.SequenceNumber)
                        .Select(link =>
                            new ProposalMilestoneResponse
                            {
                                Id = link.MilestoneId,
                                Title = link.Milestone.Title,
                                Amount = link.Milestone.Amount,
                                SequenceNumber =
                                    link.Milestone.SequenceNumber
                            })
                        .ToList()
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

                // A project keeps accepting proposals for the milestones
                // nobody has taken yet, so going InProgress after the first
                // acceptance must not close it to the remaining stages.
                if (project.Status != ProjectStatus.Open &&
                    project.Status != ProjectStatus.InProgress)
                {
                    return Conflict(new
                    {
                        message =
                            "Proposals can no longer be accepted for this project.",
                        currentStatus = project.Status
                    });
                }
                var milestoneCount =
    await dbContext.Milestones
        .CountAsync(
            milestone =>
                milestone.ProjectId ==
                projectId,
            cancellationToken
        );

                if (milestoneCount == 0)
                {
                    return Conflict(new
                    {
                        message =
                            "A proposal cannot be accepted because this project has no milestones."
                    });
                }

                var allocatedAmount =
                    await dbContext.Milestones
                        .Where(milestone =>
                            milestone.ProjectId ==
                            projectId
                        )
                        .SumAsync(
                            milestone =>
                                (decimal?)milestone.Amount,
                            cancellationToken
                        ) ?? 0m;

                if (allocatedAmount != project.Budget)
                {
                    return Conflict(new
                    {
                        message =
                            "A proposal cannot be accepted because the milestone amounts do not match the project budget.",

                        projectBudget =
                            project.Budget,

                        allocatedAmount,

                        remainingAmount =
                            project.Budget -
                            allocatedAmount
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

                var claimedMilestoneIds =
                    await dbContext.ProposalMilestones
                        .AsNoTracking()
                        .Where(link => link.ProposalId == proposalId)
                        .Select(link => link.MilestoneId)
                        .ToListAsync(cancellationToken);

                if (claimedMilestoneIds.Count == 0)
                {
                    return Conflict(new
                    {
                        message =
                            "This proposal does not name any milestone to deliver."
                    });
                }

                // Another proposal may have claimed one of these in the
                // meantime, so re-check inside the transaction.
                var alreadyAssigned = await dbContext.Milestones
                    .AsNoTracking()
                    .AnyAsync(
                        milestone =>
                            claimedMilestoneIds.Contains(milestone.Id) &&
                            milestone.FreelancerId != null,
                        cancellationToken
                    );

                if (alreadyAssigned)
                {
                    return Conflict(new
                    {
                        message =
                            "One or more milestones in this proposal have already been given to another freelancer."
                    });
                }

                // The freelancer agreed to what their chosen milestones
                // paid when they applied. If the client changed those
                // amounts afterwards, the freelancer has to see and accept
                // the new figure, so this is a rejection rather than a
                // silent re-price.
                var currentClaimedTotal = await dbContext.Milestones
                    .Where(milestone =>
                        claimedMilestoneIds.Contains(milestone.Id))
                    .SumAsync(
                        milestone => (decimal?)milestone.Amount,
                        cancellationToken
                    ) ?? 0m;

                if (proposal.BidAmount != currentClaimedTotal)
                {
                    return Conflict(new
                    {
                        message =
                            "The price of these milestones changed after this freelancer applied, so they have not agreed to the current amount. Restore the amounts they applied at, or ask them to apply again.",
                        currentTotal = currentClaimedTotal,
                        agreedPrice = proposal.BidAmount
                    });
                }

                proposal.Status = ProposalStatus.Accepted;
                project.Status = ProjectStatus.InProgress;

                await dbContext.Milestones
                    .Where(milestone =>
                        claimedMilestoneIds.Contains(milestone.Id))
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(
                            milestone => milestone.FreelancerId,
                            proposal.FreelancerId
                        ),
                        cancellationToken
                    );

                // Other freelancers may still be needed for the milestones
                // this proposal did not take, so their proposals stay open.
                // Only proposals that now overlap an assigned milestone are
                // rejected.
                var conflictingProposalIds =
                    await dbContext.ProposalMilestones
                        .AsNoTracking()
                        .Where(link =>
                            claimedMilestoneIds.Contains(
                                link.MilestoneId) &&
                            link.ProposalId != proposalId)
                        .Select(link => link.ProposalId)
                        .Distinct()
                        .ToListAsync(cancellationToken);

                if (conflictingProposalIds.Count > 0)
                {
                    await dbContext.Proposals
                        .Where(otherProposal =>
                            conflictingProposalIds.Contains(
                                otherProposal.Id) &&
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
                }

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
