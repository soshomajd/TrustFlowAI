using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
public class MilestonesController(AppDbContext dbContext)
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
            Status = milestone.Status
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


    [HttpGet("{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestoneById(
    Guid projectId,
    Guid milestoneId,
    CancellationToken cancellationToken)
    {
        var milestone = await dbContext.Milestones
     .AsNoTracking()
     .Where(milestone =>
         milestone.Id == milestoneId &&
         milestone.ProjectId == projectId)
     .Select(milestone => new MilestoneResponse
     {
         Id = milestone.Id,
         ProjectId = milestone.ProjectId,
         Title = milestone.Title,
         Description = milestone.Description,
         Amount = milestone.Amount,
         SequenceNumber = milestone.SequenceNumber,
         Deadline = milestone.Deadline,
         Status = milestone.Status
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
                milestone.Status == MileStoneStatus.Pending)
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

        var affectedRows = await dbContext.Milestones
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.FreelancerId == freelancerId &&
                milestone.Project.Status == ProjectStatus.InProgress &&
                (
                    milestone.Status == MileStoneStatus.Pending ||
                    milestone.Status == MileStoneStatus.Rejected
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

        var milestoneStatus = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone =>
                milestone.Id == milestoneId &&
                milestone.ProjectId == projectId &&
                milestone.Project.FreelancerId == freelancerId)
            .Select(milestone => new
            {
                MilestoneStatus = milestone.Status,
                ProjectStatus = milestone.Project.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (milestoneStatus is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        if (milestoneStatus.ProjectStatus != ProjectStatus.InProgress)
        {
            return Conflict(new
            {
                message =
                    "Milestones can only be started when the project is in progress.",
                currentProjectStatus =
                    milestoneStatus.ProjectStatus
            });
        }

        return Conflict(new
        {
            message =
                "Only a pending or rejected milestone can be started.",
            currentStatus =
                milestoneStatus.MilestoneStatus
        });
    }
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPatch("{milestoneId:guid}/submit")]
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
                milestone.Project.FreelancerId == freelancerId &&
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
                milestone.Project.FreelancerId == freelancerId)
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
    [HttpPatch("{milestoneId:guid}/reject")]
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
