using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models;
using System.Data;
using Npgsql;
using IsolationLevel = System.Data.IsolationLevel;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/milestones")]
public class MilestonesController(
    AppDbContext dbContext,
    IEscrowChainDeployer chainDeployer)
    : ControllerBase
{
    private static bool IsSerializationFailure(Exception exception)
    {
        Exception? currentException = exception;
        while (currentException is not null)
        {
            if (currentException is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.SerializationFailure)
            {
                return true;
            }
            currentException = currentException.InnerException;
        }

        return false;
    }

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

    private static MilestoneResponse ToResponse(MileStone milestone)
    {
        return new MilestoneResponse
        {
            Id = milestone.Id,
            ProjectId = milestone.ProjectId,
            Title = milestone.Title,
            Description = milestone.Description,
            Amount = milestone.Amount,
            SequenceNumber = milestone.SequenceNumber,
            Deadline = milestone.Deadline,
            Status = milestone.Status,
            FreelancerId = milestone.FreelancerId,
            FreelancerFullName = milestone.Freelancer?.FullName
        };
    }



    [Authorize(Roles = AppRoles.Client)]
    [HttpPost]
    public async Task<IActionResult> CreateMilestone(
      Guid projectId,
      CreateMilestoneRequest request,
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

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken
                    );

                var project = await dbContext.Projects
                    .AsNoTracking()
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
                            "Milestones can only be created while the project is open.",
                        currentProjectStatus = project.Status
                    });
                }

                // Rejected and withdrawn proposals no longer bind anyone,
                // so they release the project again.
                var hasProposals = await dbContext.Proposals
                    .AnyAsync(
                        proposal =>
                            proposal.ProjectId == projectId &&
                            (proposal.Status ==
                                 ProposalStatus.Pending ||
                             proposal.Status ==
                                 ProposalStatus.Accepted),
                        cancellationToken
                    );

                if (hasProposals)
                {
                    return Conflict(new
                    {
                        message =
                            "Milestones can no longer be added because a freelancer has already sent a proposal for this project.",
                        hasProposals = true
                    });
                }

                var sequenceExists = await dbContext.Milestones
                    .AnyAsync(
                        milestone =>
                            milestone.ProjectId == projectId &&
                            milestone.SequenceNumber ==
                            request.SequenceNumber,
                        cancellationToken
                    );

                if (sequenceExists)
                {
                    return Conflict(new
                    {
                        message = "This sequence number already exists."
                    });
                }

                var allocatedAmount = await dbContext.Milestones
                    .Where(
                        milestone =>
                            milestone.ProjectId == projectId
                    )
                    .SumAsync(
                        milestone => (decimal?)milestone.Amount,
                        cancellationToken
                    ) ?? 0m;

                if (allocatedAmount + request.Amount > project.Budget)
                {
                    return BadRequest(new
                    {
                        message = "Milestone amounts cannot exceed project budget.",
                        projectBudget = project.Budget,
                        allocatedAmount,
                        requestedAmount = request.Amount,
                        remainingBudget =
                            project.Budget - allocatedAmount
                    });
                }

                if (request.Deadline > project.Deadline)
                {
                    return BadRequest(new
                    {
                        message = "Milestone deadline cannot be after project deadline."
                    });
                }

                var milestone = new MileStone
                {
                    ProjectId = projectId,
                    Title = request.Title,
                    Description = request.Description,
                    Amount = request.Amount,
                    SequenceNumber = request.SequenceNumber,
                    Deadline = request.Deadline.ToUniversalTime()
                };

                dbContext.Milestones.Add(milestone);

                await dbContext.SaveChangesAsync(
                    cancellationToken
                );

                await transaction.CommitAsync(
                    cancellationToken
                );
                return Created(
                    $"/api/projects/{projectId}/milestones/{milestone.Id}",
                    ToResponse(milestone)
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
                    TimeSpan.FromMilliseconds(50 * attempt),
                    cancellationToken
                );
            }
            catch (Exception exception)
                when (IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message = "Another request changed this project. Please try again."
                });
            }
            catch (Exception exception)
                 when (IsUniqueViolation(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message = "This sequence number already exists."
                });
            }
        }

        return Conflict(new
        {
            message = "The milestone could not be created due to concurrent requests."
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetMilestones(
    Guid projectId,
    CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
            userIdValue,
            out var userId))
        {
            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var hasAccess = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.Id == projectId &&
                    (
                        project.ClientId == userId ||
                        project.Milestones.Any(milestone =>
                            milestone.FreelancerId == userId)
                    ),
                cancellationToken
            );

        if (!hasAccess)
        {
            return NotFound(new
            {
                message = "Project not found."
            });
        }

        var milestones = await dbContext.Milestones
            .AsNoTracking()
            // Each stage can belong to a different freelancer, and the
            // client needs the owner's name to read the list.
            .Include(milestone => milestone.Freelancer)
            .Where(milestone =>
                milestone.ProjectId ==
                    projectId
            )
            .OrderBy(milestone =>
                milestone.SequenceNumber
            )
            .ToListAsync(
                cancellationToken
            );

        var response = milestones
            .Select(ToResponse)
            .ToList();

        return Ok(response);
    }


    [Authorize]
    [HttpGet("{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestoneById(
      Guid projectId,
      Guid milestoneId,
      CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        var milestone = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                (
                    milestone.Project.ClientId == userId ||
                    milestone.FreelancerId == userId
                ))
            .Select(milestone => new MilestoneResponse
            {
                Id = milestone.Id,

                ProjectId = milestone.ProjectId,

                Title = milestone.Title,

                Description = milestone.Description,

                Amount = milestone.Amount,

                SequenceNumber =
                    milestone.SequenceNumber,

                Deadline = milestone.Deadline,

                Status = milestone.Status,

                FreelancerId = milestone.FreelancerId,

                FreelancerFullName =
                    milestone.Freelancer == null
                        ? null
                        : milestone.Freelancer.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestone is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        return Ok(milestone);
    }
    [Authorize(Roles = AppRoles.Client)]
    [HttpPut("{milestoneId:guid}")]
    public async Task<IActionResult> UpdateMilestone(
      Guid projectId,
      Guid milestoneId,
      UpdateMilestoneRequest request,
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

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken
                    );

                var project = await dbContext.Projects
                    .AsNoTracking()
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
                            "Milestones can only be updated while the project is open.",
                        currentProjectStatus = project.Status
                    });
                }

                // Rejected and withdrawn proposals no longer bind anyone,
                // so they release the project again.
                var hasProposals = await dbContext.Proposals
                    .AnyAsync(
                        proposal =>
                            proposal.ProjectId == projectId &&
                            (proposal.Status ==
                                 ProposalStatus.Pending ||
                             proposal.Status ==
                                 ProposalStatus.Accepted),
                        cancellationToken
                    );

                if (hasProposals)
                {
                    return Conflict(new
                    {
                        message =
                            "Milestones can no longer be changed because a freelancer has already sent a proposal for this project.",
                        hasProposals = true
                    });
                }

                var milestone = await dbContext.Milestones
                    .FirstOrDefaultAsync(
                        milestone =>
                            milestone.Id == milestoneId &&
                            milestone.ProjectId == projectId,
                        cancellationToken
                    );

                if (milestone is null)
                {
                    return NotFound(new
                    {
                        message = "Milestone not found."
                    });
                }
                if (milestone.Status != MileStoneStatus.Pending)
                {
                    return Conflict(new
                    {
                        message =
                            "Only a pending milestone can be updated.",
                        currentMilestoneStatus = milestone.Status
                    });
                }

                var sequenceExists = await dbContext.Milestones
                    .AnyAsync(
                        otherMilestone =>
                            otherMilestone.ProjectId == projectId &&
                            otherMilestone.SequenceNumber ==
                                request.SequenceNumber &&
                            otherMilestone.Id != milestoneId,
                        cancellationToken
                    );

                if (sequenceExists)
                {
                    return Conflict(new
                    {
                        message =
                            "This sequence number already exists."
                    });
                }

                var otherMilestonesAmount =
                    await dbContext.Milestones
                        .Where(otherMilestone =>
                            otherMilestone.ProjectId == projectId &&
                            otherMilestone.Id != milestoneId)
                        .SumAsync(
                            otherMilestone =>
                                (decimal?)otherMilestone.Amount,
                            cancellationToken
                        ) ?? 0m;

                if (otherMilestonesAmount + request.Amount >
                    project.Budget)
                {
                    return BadRequest(new
                    {
                        message =
                            "Milestone amounts cannot exceed project budget.",
                        projectBudget = project.Budget,
                        allocatedAmount = otherMilestonesAmount,
                        requestedAmount = request.Amount,
                        remainingBudget =
                            project.Budget - otherMilestonesAmount
                    });
                }

                if (request.Deadline > project.Deadline)
                {
                    return BadRequest(new
                    {
                        message =
                            "Milestone deadline cannot be after project deadline."
                    });
                }

                milestone.Title = request.Title;
                milestone.Description = request.Description;
                milestone.Amount = request.Amount;
                milestone.SequenceNumber = request.SequenceNumber;
                milestone.Deadline = request.Deadline.ToUniversalTime();

                await dbContext.SaveChangesAsync(
                    cancellationToken
                );

                await transaction.CommitAsync(
                    cancellationToken
                );

                return Ok(ToResponse(milestone));
            }
            catch (Exception exception)
                when (
                    IsSerializationFailure(exception) &&
                    attempt < maxAttempts
                )
            {
                dbContext.ChangeTracker.Clear();

                await Task.Delay(
                    TimeSpan.FromMilliseconds(50 * attempt),
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
            catch (Exception exception)
             when (IsUniqueViolation(exception))
            {
                dbContext.ChangeTracker.Clear();

                return Conflict(new
                {
                    message =
                        "This sequence number already exists."
                });
            }
        }

        return Conflict(new
        {
            message =
                "The milestone could not be updated due to concurrent requests."
        });
    }
    [Authorize(Roles = AppRoles.Client)]
    [HttpDelete("{milestoneId:guid}")]
    public async Task<IActionResult> DeleteMilestone(
       Guid projectId,
       Guid milestoneId,
       CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var affectedRows = await dbContext.Milestones
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.ClientId == clientId &&
                milestone.Project.Status == ProjectStatus.Open &&
                milestone.Status == MileStoneStatus.Pending &&
                // Removing a stage after someone applied would change the
                // scope and the payment split they agreed to.
                !milestone.Project.Proposals.Any(proposal =>
                    proposal.Status == ProposalStatus.Pending ||
                    proposal.Status == ProposalStatus.Accepted))
            .ExecuteDeleteAsync(cancellationToken);

        if (affectedRows == 1)
        {
            return NoContent();
        }

        var milestoneInfo = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.ClientId == clientId)
            .Select(milestone => new
            {
                ProjectStatus = milestone.Project.Status,
                MilestoneStatus = milestone.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestoneInfo is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (milestoneInfo.ProjectStatus != ProjectStatus.Open)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be deleted while the project is open.",
                currentProjectStatus =
                    milestoneInfo.ProjectStatus
            });
        }

        var hasProposals = await dbContext.Proposals
            .AnyAsync(
                proposal =>
                    proposal.ProjectId == projectId &&
                    (proposal.Status == ProposalStatus.Pending ||
                     proposal.Status == ProposalStatus.Accepted),
                cancellationToken
            );

        if (hasProposals)
        {
            return Conflict(new
            {
                message =
                    "Milestones can no longer be deleted because a freelancer has already sent a proposal for this project.",
                hasProposals = true
            });
        }

        return Conflict(new
        {
            message =
                "Only a pending milestone can be deleted.",
            currentMilestoneStatus =
                milestoneInfo.MilestoneStatus
        });
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPatch("{milestoneId:guid}/start")]
    public async Task<IActionResult> StartMilestone(
        Guid projectId,
        Guid milestoneId,
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

        var workStartGate = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.FreelancerId == freelancerId)
            .Select(milestone => new
            {
                EscrowStatus = milestone.Project.Escrows
                    .Where(escrow =>
                        escrow.FreelancerId == milestone.FreelancerId)
                    .Select(escrow => (EscrowStatus?)escrow.Status)
                    .FirstOrDefault(),

                ContractAddress = milestone.Project.Escrows
                    .Where(escrow =>
                        escrow.FreelancerId == milestone.FreelancerId)
                    .Select(escrow => escrow.ContractAddress)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (workStartGate?.EscrowStatus == EscrowStatus.Funded &&
            !string.IsNullOrWhiteSpace(workStartGate.ContractAddress))
        {
            var onChainEscrow = await chainDeployer
                .GetEscrowSnapshotAsync(
                    workStartGate.ContractAddress,
                    cancellationToken
                );

            if (onChainEscrow.State != OnChainEscrowState.Funded ||
                !onChainEscrow.WorkStarted)
            {
                return Conflict(new
                {
                    message =
                        "Start work in the freelancer wallet before starting a milestone.",
                    workStartedOnChain =
                        onChainEscrow.WorkStarted,
                    onChainEscrowState =
                        onChainEscrow.State
                });
            }
        }

        var affectedRows = await dbContext.Milestones
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.FreelancerId == freelancerId &&
                milestone.Project.Status == ProjectStatus.InProgress &&
                milestone.Project.Escrows.Any(escrow =>
                    escrow.FreelancerId == milestone.FreelancerId &&
                    escrow.Status == EscrowStatus.Funded) &&
                (
                    milestone.Status == MileStoneStatus.Pending ||
                    milestone.Status == MileStoneStatus.Rejected
                ) &&
                // Each escrow releases its own milestones in order, so the
                // queue is per freelancer. One freelancer running late must
                // not block another's stage.
                !dbContext.Milestones.Any(previousMilestone =>
                    previousMilestone.ProjectId ==
                        milestone.ProjectId &&
                    previousMilestone.FreelancerId ==
                        milestone.FreelancerId &&
                    previousMilestone.SequenceNumber <
                        milestone.SequenceNumber &&
                    previousMilestone.Status !=
                        MileStoneStatus.Approved
                ))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    milestone => milestone.Status,
                    MileStoneStatus.InProgress
                ),
                cancellationToken
            );

        if (affectedRows == 1)
        {
            return Ok(new
            {
                id = milestoneId,
                status = MileStoneStatus.InProgress
            });
        }

        var milestoneInfo = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.FreelancerId == freelancerId)
            .Select(milestone => new
            {
                MilestoneStatus = milestone.Status,
                ProjectStatus = milestone.Project.Status,

                EscrowStatus = milestone.Project.Escrows
                    .Where(escrow =>
                        escrow.FreelancerId == milestone.FreelancerId)
                    .Select(escrow => (EscrowStatus?)escrow.Status)
                    .FirstOrDefault(),

                milestone.SequenceNumber
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestoneInfo is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (milestoneInfo.ProjectStatus !=
            ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be started when the project is in progress.",
                currentProjectStatus =
                    milestoneInfo.ProjectStatus
            });
        }

        if (milestoneInfo.EscrowStatus != EscrowStatus.Funded)
        {
            return Conflict(new
            {
                message =
                    "Work cannot start until the project's escrow is funded.",
                currentEscrowStatus =
                    milestoneInfo.EscrowStatus
            });
        }

        if (milestoneInfo.MilestoneStatus !=
                MileStoneStatus.Pending &&
            milestoneInfo.MilestoneStatus !=
                MileStoneStatus.Rejected)
        {
            return Conflict(new
            {
                message =
                    "Only a pending or rejected milestone can be started.",
                currentStatus =
                    milestoneInfo.MilestoneStatus
            });
        }

        var blockingMilestone = await dbContext.Milestones
            .AsNoTracking()
            .Where(previousMilestone =>
                previousMilestone.ProjectId == projectId &&
                previousMilestone.SequenceNumber <
                    milestoneInfo.SequenceNumber &&
                previousMilestone.Status !=
                    MileStoneStatus.Approved)
            .OrderBy(previousMilestone =>
                previousMilestone.SequenceNumber)
            .Select(previousMilestone => new
            {
                previousMilestone.Id,
                previousMilestone.SequenceNumber,
                previousMilestone.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (blockingMilestone is not null)
        {
            return Conflict(new
            {
                message =
                    "Previous milestones must be approved before this milestone can be started.",
                blockingMilestoneId =
                    blockingMilestone.Id,
                blockingSequenceNumber =
                    blockingMilestone.SequenceNumber,
                blockingStatus =
                    blockingMilestone.Status
            });
        }

        return Conflict(new
        {
            message =
                "The milestone could not be started because its state changed."
        });
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [NonAction]
    public async Task<IActionResult> SubmitMilestone(
    Guid projectId,
    Guid milestoneId,
    CancellationToken cancellationToken)
    {
        var freelancerIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(freelancerIdValue, out var freelancerId))
        {
            return Unauthorized();
        }

        var affectedRows = await dbContext.Milestones
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.FreelancerId == freelancerId &&
                milestone.Project.Status == ProjectStatus.InProgress &&
                milestone.Status == MileStoneStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    milestone => milestone.Status,
                    MileStoneStatus.Submitted
                ),
                cancellationToken
            );

        if (affectedRows == 1)
        {
            return Ok(new MilestoneStatusResponse
            {
                Id = milestoneId,
                Status = MileStoneStatus.Submitted
            });
        }

        var milestoneInfo = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.FreelancerId == freelancerId)
            .Select(milestone => new
            {
                MilestoneStatus = milestone.Status,
                ProjectStatus = milestone.Project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestoneInfo is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (milestoneInfo.ProjectStatus !=
            ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be submitted when the project is in progress.",
                currentProjectStatus =
                    milestoneInfo.ProjectStatus
            });
        }

        return Conflict(new
        {
            message =
                "Only an in-progress milestone can be submitted.",
            currentStatus =
                milestoneInfo.MilestoneStatus
        });
    }
    [Authorize(Roles = AppRoles.Client)]
    [HttpPatch("{milestoneId:guid}/approve")]
    public async Task<IActionResult> ApproveMilestone(
      Guid projectId,
      Guid milestoneId,
      CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var preCheck = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.ClientId == clientId)
            .Select(milestone => new
            {
                milestone.Status,
                milestone.Amount,
                milestone.SequenceNumber,
                milestone.FreelancerId,
                ProjectStatus = milestone.Project.Status,

                // Payment comes from the escrow of the freelancer who owns
                // this milestone, not from a single project-wide one.
                Escrow = milestone.Project.Escrows
                    .Where(escrow =>
                        escrow.FreelancerId == milestone.FreelancerId)
                    .Select(escrow => new
                    {
                        escrow.ContractAddress,
                        escrow.TokenAddress
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (preCheck is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (preCheck.ProjectStatus != ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be approved when the project is in progress.",
                currentProjectStatus = preCheck.ProjectStatus
            });
        }

        if (preCheck.Status != MileStoneStatus.Submitted)
        {
            return Conflict(new
            {
                message =
                    "Only a submitted milestone can be approved.",
                currentStatus = preCheck.Status
            });
        }

        if (preCheck.Escrow is null ||
            string.IsNullOrWhiteSpace(
                preCheck.Escrow.ContractAddress))
        {
            return Conflict(new
            {
                message =
                    "The project's escrow has not been deployed."
            });
        }

        var onChainIndex = await dbContext.Milestones
            .Where(otherMilestone =>
                otherMilestone.ProjectId == projectId &&
                otherMilestone.SequenceNumber <
                    preCheck.SequenceNumber)
            .CountAsync(cancellationToken);

        var tokenDecimals =
            await chainDeployer.GetTokenDecimalsAsync(
                preCheck.Escrow.TokenAddress,
                cancellationToken
            );

        var expectedBaseUnits = TokenAmountConverter.ToBaseUnits(
            preCheck.Amount,
            tokenDecimals
        );

        var onChainMilestone =
            await chainDeployer.GetMilestoneAsync(
                preCheck.Escrow.ContractAddress,
                onChainIndex,
                cancellationToken
            );

        if (!onChainMilestone.Released)
        {
            return Conflict(new
            {
                message =
                    "The client's wallet has not released this " +
                    "milestone on-chain yet. Call the contract's " +
                    "releaseMilestone() before approving.",
                onChainMilestoneIndex = onChainIndex
            });
        }

        if (onChainMilestone.Amount != expectedBaseUnits)
        {
            throw new InvalidOperationException(
                $"On-chain milestone {onChainIndex} amount " +
                $"{onChainMilestone.Amount} does not match the " +
                $"expected {expectedBaseUnits} for milestone " +
                $"{milestoneId}."
            );
        }

        // On-chain "released" and escrow "Completed" are monotonic (the
        // contract never un-releases a milestone or leaves Completed),
        // so it is safe to have verified them above, outside any DB
        // transaction, before performing the actual state transition.
        var escrowSnapshot =
            await chainDeployer.GetEscrowSnapshotAsync(
                preCheck.Escrow.ContractAddress,
                cancellationToken
            );

        var escrowCompletedOnChain =
            escrowSnapshot.State == OnChainEscrowState.Completed;

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
                            IsolationLevel.Serializable,
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

                if (project.Status != ProjectStatus.InProgress)
                {
                    return Conflict(new
                    {
                        message =
                            "Milestones can only be approved when the project is in progress.",
                        currentProjectStatus = project.Status
                    });
                }

                var milestone = await dbContext.Milestones
                    .FirstOrDefaultAsync(
                        milestone =>
                            milestone.Id == milestoneId &&
                            milestone.ProjectId == projectId,
                        cancellationToken
                    );

                if (milestone is null)
                {
                    return NotFound(new
                    {
                        message = "Milestone not found."
                    });
                }

                if (milestone.Status !=
                    MileStoneStatus.Submitted)
                {
                    return Conflict(new
                    {
                        message =
                            "Only a submitted milestone can be approved.",
                        currentStatus = milestone.Status
                    });
                }

                // One escrow per freelancer, so the release has to be
                // credited to the escrow that pays this milestone's owner.
                var escrow = await dbContext.Escrows
                    .FirstOrDefaultAsync(
                        item =>
                            item.ProjectId == projectId &&
                            item.FreelancerId ==
                                milestone.FreelancerId,
                        cancellationToken
                    );

                if (escrow is null)
                {
                    return Conflict(new
                    {
                        message =
                            "The project's escrow has not been deployed."
                    });
                }

                // This escrow is finished once every stage it pays for is
                // approved.
                var hasOtherUnapprovedMilestonesForFreelancer =
                    await dbContext.Milestones
                        .AnyAsync(
                            otherMilestone =>
                                otherMilestone.ProjectId ==
                                    projectId &&
                                otherMilestone.FreelancerId ==
                                    milestone.FreelancerId &&
                                otherMilestone.Id !=
                                    milestoneId &&
                                otherMilestone.Status !=
                                    MileStoneStatus.Approved,
                            cancellationToken
                        );

                // The project is finished only once every stage is approved,
                // including those held by other freelancers.
                var hasOtherUnapprovedMilestones =
                    await dbContext.Milestones
                        .AnyAsync(
                            otherMilestone =>
                                otherMilestone.ProjectId ==
                                    projectId &&
                                otherMilestone.Id !=
                                    milestoneId &&
                                otherMilestone.Status !=
                                    MileStoneStatus.Approved,
                            cancellationToken
                        );

                milestone.Status =
                    MileStoneStatus.Approved;

                var now = DateTimeOffset.UtcNow;

                escrow.ReleasedAmount += milestone.Amount;
                escrow.UpdatedAt = now;

                if (!hasOtherUnapprovedMilestonesForFreelancer)
                {
                    if (!escrowCompletedOnChain)
                    {
                        throw new InvalidOperationException(
                            $"Escrow {escrow.Id} is expected to " +
                            "be Completed on-chain after its last " +
                            "milestone released, but it was not."
                        );
                    }

                    escrow.Status = EscrowStatus.Completed;
                    escrow.CompletedAt = now;
                }

                if (!hasOtherUnapprovedMilestones)
                {
                    project.Status =
                        ProjectStatus.Completed;
                }

                await dbContext.SaveChangesAsync(
                    cancellationToken
                );

                await transaction.CommitAsync(
                    cancellationToken
                );

                return Ok(new ApproveMilestoneResponse
                {
                    Id = milestone.Id,
                    ProjectId = project.Id,
                    Status = milestone.Status,
                    ProjectStatus = project.Status
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
                "The milestone could not be approved due to concurrent requests."
        });
    }
    [Authorize(Roles = AppRoles.Client)]
    [NonAction]
    public async Task<IActionResult> RejectMilestone(
    Guid projectId,
    Guid milestoneId,
    CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var affectedRows = await dbContext.Milestones
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.ClientId == clientId &&
                milestone.Project.Status == ProjectStatus.InProgress &&
                milestone.Status == MileStoneStatus.Submitted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    milestone => milestone.Status,
                    MileStoneStatus.Rejected
                ),
                cancellationToken
            );

        if (affectedRows == 1)
        {
            return Ok(new MilestoneStatusResponse
            {
                Id = milestoneId,
                Status = MileStoneStatus.Rejected
            });
        }

        var milestoneInfo = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.ClientId == clientId)
            .Select(milestone => new
            {
                MilestoneStatus = milestone.Status,
                ProjectStatus = milestone.Project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestoneInfo is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (milestoneInfo.ProjectStatus !=
            ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be rejected when the project is in progress.",
                currentProjectStatus =
                    milestoneInfo.ProjectStatus
            });
        }

        return Conflict(new
        {
            message =
                "Only a submitted milestone can be rejected.",
            currentStatus =
                milestoneInfo.MilestoneStatus
        });
    }
}
