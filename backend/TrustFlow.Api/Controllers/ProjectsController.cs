
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Projects;
using TrustFlow.Api.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TrustFlow.Api.Constants;
using System.Data;
using Npgsql;
using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Dtos.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace TrustFlow.Api.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController(AppDbContext dbContext) : ControllerBase
    {

        private static bool IsSerializationFailure(Exception exception)
        {
            Exception? currentException = exception;

            while (currentException is not null)
            {
                if (currentException is PostgresException postgresException &&
                    postgresException.SqlState ==
                    PostgresErrorCodes.SerializationFailure)
                {
                    return true;
                }

                currentException = currentException.InnerException;
            }

            return false;
        }
        [HttpPost]
        [Authorize(Roles = AppRoles.Client)]
        public async Task<IActionResult> CreateProject(CreateProjectRequest request, CancellationToken cancellationToken)
        {
            var clientIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(clientIdValue, out var clientId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }
            var project = new Project
            {
                ClientId = clientId,
                Title = request.Title,
                Description = request.Description,
                Budget = request.Budget,
                Deadline = request.Deadline.ToUniversalTime()
            };

            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Created($"/api/projects/{project.Id}", project);
        }


        [EnableRateLimiting(RateLimitPolicies.PublicMarketplace)]
        [HttpGet]
        public async Task<IActionResult> GetProjects(
       [FromQuery] ProjectMarketplaceQuery request,
       CancellationToken cancellationToken)
        {
            var query = dbContext.Projects
      .AsNoTracking()
      .Where(project =>
          project.Status == ProjectStatus.Open &&
          project.Milestones.Any() &&
          project.Milestones.Sum(
              milestone => milestone.Amount
          ) == project.Budget
      );

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchTerm = request.Search.Trim();

                query = query.Where(project =>
                    EF.Functions.ILike(
                        project.Title,
                        $"%{searchTerm}%"
                    ) ||
                    EF.Functions.ILike(
                        project.Description,
                        $"%{searchTerm}%"
                    )
                );
            }

            if (request.MinBudget.HasValue)
            {
                query = query.Where(project =>
                    project.Budget >=
                    request.MinBudget.Value
                );
            }

            if (request.MaxBudget.HasValue)
            {
                query = query.Where(project =>
                    project.Budget <=
                    request.MaxBudget.Value
                );
            }

            if (request.DeadlineBefore.HasValue)
            {
                var deadlineBeforeUtc =
                    request.DeadlineBefore.Value
                        .ToUniversalTime();

                query = query.Where(project =>
                    project.Deadline <= deadlineBeforeUtc
                );
            }

            var totalItems = await query.CountAsync(
                cancellationToken
            );

            query = request.SortBy switch
            {
                ProjectSortOption.Oldest =>
                    query
                        .OrderBy(project =>
                            project.CreatedAt)
                        .ThenBy(project =>
                            project.Id),

                ProjectSortOption.BudgetLowToHigh =>
                    query
                        .OrderBy(project =>
                            project.Budget)
                        .ThenByDescending(project =>
                            project.CreatedAt),

                ProjectSortOption.BudgetHighToLow =>
                    query
                        .OrderByDescending(project =>
                            project.Budget)
                        .ThenByDescending(project =>
                            project.CreatedAt),

                ProjectSortOption.DeadlineSoonest =>
                    query
                        .OrderBy(project =>
                            project.Deadline)
                        .ThenByDescending(project =>
                            project.CreatedAt),

                ProjectSortOption.DeadlineLatest =>
                    query
                        .OrderByDescending(project =>
                            project.Deadline)
                        .ThenByDescending(project =>
                            project.CreatedAt),

                _ =>
                    query
                        .OrderByDescending(project =>
                            project.CreatedAt)
                        .ThenByDescending(project =>
                            project.Id)
            };

            var items = await query
                .Skip(
                    (request.Page - 1) *
                    request.PageSize
                )
                .Take(request.PageSize)
                .Select(project =>
                    new ProjectSummaryResponse
                    {
                        Id = project.Id,
                        Title = project.Title,
                        Description = project.Description,
                        Budget = project.Budget,

                        AllocatedAmount =
                            project.Milestones
                                .Sum(milestone =>
                                    (decimal?)milestone.Amount)
                            ?? 0m,

                        MilestoneCount =
                            project.Milestones.Count,

                        Deadline = project.Deadline,
                        CreatedAt = project.CreatedAt,
                        Status = project.Status
                    })
                .ToListAsync(cancellationToken);

            var totalPages = (int)Math.Ceiling(
                totalItems /
                (double)request.PageSize
            );

            var response =
                new PagedResponse<ProjectSummaryResponse>
                {
                    Items = items,
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalItems = totalItems,
                    TotalPages = totalPages,
                    HasPreviousPage = request.Page > 1,
                    HasNextPage = request.Page < totalPages
                };

            return Ok(response);
        }
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProjectById(
             Guid id,
            CancellationToken cancellationToken)
        {
            var project = await dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                project.Id == id &&
                project.Status == ProjectStatus.Open &&
                project.Milestones.Any() &&
                 project.Milestones.Sum(
        milestone => milestone.Amount
    ) == project.Budget
)
                .Select(project =>
                    new PublicProjectDetailsResponse
                    {
                        Id = project.Id,

                        Title = project.Title,

                        Description = project.Description,

                        Budget = project.Budget,

                        AllocatedAmount =
                            project.Milestones
                                .Sum(milestone =>
                                    (decimal?)milestone.Amount)
                            ?? 0m,

                        Deadline = project.Deadline,

                        CreatedAt = project.CreatedAt,

                        Status = project.Status,

                        Milestones = project.Milestones
                            .OrderBy(milestone =>
                                milestone.SequenceNumber)
                            .Select(milestone =>
                                new PublicMilestoneResponse
                                {
                                    Id = milestone.Id,

                                    Title = milestone.Title,

                                    Description =
                                        milestone.Description,

                                    Amount = milestone.Amount,

                                    SequenceNumber =
                                        milestone.SequenceNumber,

                                    Deadline =
                                        milestone.Deadline
                                })
                            .ToList()
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (project is null)
            {
                return NotFound(new
                {
                    message =
                        "Open project not found."
                });
            }

            return Ok(project);
        }
        [Authorize(Roles = AppRoles.Client)]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdateProject(
      Guid id,
      UpdateProjectRequest request,
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
                        .FirstOrDefaultAsync(
                            project =>
                                project.Id == id &&
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
                                "Only an open project can be updated.",
                            currentProjectStatus = project.Status
                        });
                    }

                    var allocatedAmount = await dbContext.Milestones
                        .Where(milestone =>
                            milestone.ProjectId == id)
                        .SumAsync(
                            milestone => (decimal?)milestone.Amount,
                            cancellationToken
                        ) ?? 0m;

                    if (request.Budget < allocatedAmount)
                    {
                        return BadRequest(new
                        {
                            message =
                                "Project budget cannot be less than the total milestone amount.",
                            requestedBudget = request.Budget,
                            allocatedAmount
                        });
                    }

                    var latestMilestoneDeadline =
                        await dbContext.Milestones
                            .Where(milestone =>
                                milestone.ProjectId == id)
                            .MaxAsync(
                                milestone =>
                                    (DateTimeOffset?)milestone.Deadline,
                                cancellationToken
                            );

                    if (latestMilestoneDeadline.HasValue &&
                        request.Deadline <
                        latestMilestoneDeadline.Value)
                    {
                        return BadRequest(new
                        {
                            message =
                                "Project deadline cannot be before the latest milestone deadline.",
                            requestedDeadline = request.Deadline,
                            latestMilestoneDeadline =
                                latestMilestoneDeadline.Value
                        });
                    }

                    project.Title = request.Title;
                    project.Description = request.Description;
                    project.Budget = request.Budget;
                    project.Deadline = request.Deadline.ToUniversalTime();

                    await dbContext.SaveChangesAsync(
                        cancellationToken
                    );

                    await transaction.CommitAsync(
                        cancellationToken
                    );

                    return Ok(project);
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
            }

            return Conflict(new
            {
                message =
                    "The project could not be updated due to concurrent requests."
            });
        }

        [Authorize(Roles = AppRoles.Client)]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProject(
     Guid id,
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
                        await dbContext.Database.BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable,
                            cancellationToken
                        );

                    var project = await dbContext.Projects
                        .FirstOrDefaultAsync(
                            project =>
                                project.Id == id &&
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
                                "Only an open project can be deleted.",
                            currentProjectStatus = project.Status
                        });
                    }

                    dbContext.Projects.Remove(project);

                    await dbContext.SaveChangesAsync(
                        cancellationToken
                    );

                    await transaction.CommitAsync(
                        cancellationToken
                    );

                    return NoContent();
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
                    "The project could not be deleted due to concurrent requests."
            });
        }
        [Authorize(Roles = AppRoles.Freelancer)]
        [HttpGet("assigned-to-me")]
        public async Task<IActionResult> GetAssignedProjects(
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

            var query = dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    project.FreelancerId == freelancerId);

            var totalItems = await query.CountAsync(
                cancellationToken
            );

            var items = await query
                .OrderByDescending(project =>
                    project.CreatedAt)
                .Skip(
                    (pagination.Page - 1) *
                    pagination.PageSize
                )
                .Take(pagination.PageSize)
                .Select(project =>
                    new AssignedProjectResponse
                    {
                        Id = project.Id,

                        ClientId = project.ClientId!.Value,

                        ClientFullName =
                            project.Client == null
                                ? string.Empty
                                : project.Client.FullName,

                        Title = project.Title,

                        Description = project.Description,

                        Budget = project.Budget,

                        AllocatedAmount =
                            project.Milestones
                                .Sum(milestone =>
                                    (decimal?)milestone.Amount)
                            ?? 0m,

                        MilestoneCount =
                            project.Milestones.Count,

                        RejectedMilestoneCount =
                            project.Milestones.Count(
                              milestone =>
                                 milestone.Status ==
                                  MileStoneStatus.Rejected
                          ),

                        Deadline = project.Deadline,

                        Status = project.Status,

                        CreatedAt = project.CreatedAt
                    })
                .ToListAsync(cancellationToken);

            var totalPages = (int)Math.Ceiling(
                totalItems /
                (double)pagination.PageSize
            );

            var response =
                new PagedResponse<AssignedProjectResponse>
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
        [Authorize]
        [HttpGet("{id:guid}/workspace")]
        public async Task<IActionResult> GetProjectWorkspace(
    Guid id,
    CancellationToken cancellationToken)
        {
            var userIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    project.Id == id &&
                    (
                        project.ClientId == userId ||
                        project.FreelancerId == userId
                    ))
                .Select(project =>
                    new ProjectWorkspaceResponse
                    {
                        Id = project.Id,

                        ClientId = project.ClientId,

                        ClientFullName =
                            project.Client == null
                                ? string.Empty
                                : project.Client.FullName,

                        FreelancerId =
                            project.FreelancerId,

                        FreelancerFullName =
                            project.Freelancer == null
                                ? null
                                : project.Freelancer.FullName,

                        Title = project.Title,

                        Description = project.Description,

                        Budget = project.Budget,

                        Deadline = project.Deadline,

                        Status = project.Status,

                        CreatedAt = project.CreatedAt,

                        Milestones = project.Milestones
                            .OrderBy(milestone =>
                                milestone.SequenceNumber)
                            .Select(milestone =>
                                new MilestoneResponse
                                {
                                    Id = milestone.Id,
                                    ProjectId =
                                        milestone.ProjectId,
                                    Title = milestone.Title,
                                    Description =
                                        milestone.Description,
                                    Amount = milestone.Amount,
                                    SequenceNumber =
                                        milestone.SequenceNumber,
                                    Deadline =
                                        milestone.Deadline,
                                    Status =
                                        milestone.Status
                                })
                            .ToList()
                    })
                .FirstOrDefaultAsync(cancellationToken);

            if (project is null)
            {
                return NotFound(new
                {
                    message = "Project workspace not found."
                });
            }

            return Ok(project);
        }
        [Authorize(Roles = AppRoles.Client)]
        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetClientDashboardSummary(
    CancellationToken cancellationToken)
        {
            var clientIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(
                clientIdValue,
                out var clientId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }

            var summary = await dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    project.ClientId == clientId)
                .GroupBy(project => 1)
                .Select(projects =>
                    new ClientDashboardSummaryResponse
                    {
                        TotalProjects =
                            projects.Count(),

                        OpenProjects =
                            projects.Count(project =>
                                project.Status ==
                                ProjectStatus.Open),

                        InProgressProjects =
                            projects.Count(project =>
                                project.Status ==
                                ProjectStatus.InProgress),

                        CompletedProjects =
                            projects.Count(project =>
                                project.Status ==
                                ProjectStatus.Completed),

                        TotalBudget =
                            projects.Sum(project =>
                                project.Budget)
                    })
                .FirstOrDefaultAsync(
                    cancellationToken
                )
                ?? new ClientDashboardSummaryResponse();

            summary.PendingProposals =
                await dbContext.Proposals
                    .AsNoTracking()
                    .CountAsync(
                        proposal =>
                            proposal.Project.ClientId ==
                            clientId &&
                            proposal.Status ==
                            ProposalStatus.Pending,
                        cancellationToken
                    );

            return Ok(summary);
        }


        [Authorize(Roles = AppRoles.Freelancer)]
        [HttpGet("freelancer-dashboard-summary")]
        public async Task<IActionResult>
    GetFreelancerDashboardSummary(
        CancellationToken cancellationToken)
        {
            var freelancerIdValue =
                User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

            if (!Guid.TryParse(
                freelancerIdValue,
                out var freelancerId))
            {
                return Unauthorized(new
                {
                    message =
                        "Invalid user identity."
                });
            }

            var summary =
                new FreelancerDashboardSummaryResponse
                {
                    TotalProposals =
                        await dbContext.Proposals
                            .AsNoTracking()
                            .CountAsync(
                                proposal =>
                                    proposal.FreelancerId ==
                                    freelancerId,
                                cancellationToken
                            ),

                    PendingProposals =
                        await dbContext.Proposals
                            .AsNoTracking()
                            .CountAsync(
                                proposal =>
                                    proposal.FreelancerId ==
                                    freelancerId &&
                                    proposal.Status ==
                                    ProposalStatus.Pending,
                                cancellationToken
                            ),

                    AssignedProjects =
                        await dbContext.Projects
                            .AsNoTracking()
                            .CountAsync(
                                project =>
                                    project.FreelancerId ==
                                    freelancerId,
                                cancellationToken
                            ),

                    RejectedMilestones =
                        await dbContext.Milestones
                            .AsNoTracking()
                            .CountAsync(
                                milestone =>
                                    milestone.Project
                                        .FreelancerId ==
                                    freelancerId &&
                                    milestone.Status ==
                                    MileStoneStatus.Rejected,
                                cancellationToken
                            )
                };

            return Ok(summary);
        }
        [Authorize(Roles = AppRoles.Client)]
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyProjects(
    [FromQuery] PaginationRequest pagination,
    [FromQuery] ProjectStatus? status,
    CancellationToken cancellationToken)
        {
            var clientIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(
                clientIdValue,
                out var clientId))
            {
                return Unauthorized();
            }

            var query = dbContext.Projects
                .AsNoTracking()
                .Where(project =>
                    project.ClientId == clientId);

            if (status.HasValue)
            {
                query = query.Where(project =>
                    project.Status == status.Value);
            }

            var totalItems = await query.CountAsync(
                cancellationToken
            );

            var items = await query
                .OrderByDescending(project =>
                    project.CreatedAt)
                .ThenByDescending(project =>
                    project.Id)
                .Skip(
                    (pagination.Page - 1) *
                    pagination.PageSize
                )
                .Take(pagination.PageSize)
                .Select(project =>
                    new ClientProjectResponse
                    {
                        Id = project.Id,

                        FreelancerId = project.FreelancerId,

                        FreelancerFullName =
                            project.Freelancer == null
                                ? null
                                : project.Freelancer.FullName,

                        Title = project.Title,

                        Description = project.Description,

                        Budget = project.Budget,

                        AllocatedAmount =
                            project.Milestones
                                .Sum(milestone =>
                                    (decimal?)milestone.Amount)
                            ?? 0m,

                        MilestoneCount =
                            project.Milestones.Count,

                        ApprovedMilestoneCount =
                            project.Milestones.Count(
                                milestone =>
                                    milestone.Status ==
                                    MileStoneStatus.Approved
                            ),
                        SubmittedMilestoneCount =
                            project.Milestones.Count(
                                     milestone =>
                                     milestone.Status == MileStoneStatus.Submitted
    ),

                        ProposalCount =
                            project.Proposals.Count,

                        PendingProposalCount =
                            project.Proposals.Count(
                                proposal =>
                                    proposal.Status ==
                                    ProposalStatus.Pending
                            ),

                        Deadline = project.Deadline,

                        Status = project.Status,

                        CreatedAt = project.CreatedAt
                    })
                .ToListAsync(cancellationToken);

            var totalPages = (int)Math.Ceiling(
                totalItems /
                (double)pagination.PageSize
            );

            var response =
                new PagedResponse<ClientProjectResponse>
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
    }
}
