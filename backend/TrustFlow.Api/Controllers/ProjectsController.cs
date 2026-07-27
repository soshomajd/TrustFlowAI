
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

        [HttpGet]
        public async Task<IActionResult> GetProjects(
     CancellationToken cancellationToken)
        {
            var projects = await dbContext.Projects
                .AsNoTracking()
                .OrderByDescending(project => project.CreatedAt)
                .Select(project => new ProjectSummaryResponse
                {
                    Id = project.Id,
                    Title = project.Title,
                    Description = project.Description,
                    Budget = project.Budget,

                    AllocatedAmount = project.Milestones
                        .Sum(milestone => (decimal?)milestone.Amount) ?? 0m,

                    MilestoneCount = project.Milestones.Count,

                    Deadline = project.Deadline,
                    CreatedAt = project.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return Ok(projects);
        }
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProjectById(
       Guid id,
       CancellationToken cancellationToken)
        {
            var project = await dbContext.Projects
                .AsNoTracking()
                .Where(project => project.Id == id)
                .Select(project => new ProjectDetailsResponse
                {
                    Id = project.Id,
                    Title = project.Title,
                    Description = project.Description,
                    Budget = project.Budget,
                    Deadline = project.Deadline,
                    CreatedAt = project.CreatedAt,

                    Milestones = project.Milestones
                        .OrderBy(milestone => milestone.SequenceNumber)
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
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (project is null)
            {
                return NotFound(new
                {
                    message = "Project not found."
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

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]

        public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
        {
            var ClientValueId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );
            if (!Guid.TryParse(ClientValueId, out var clientId))
            {
                return Unauthorized();
            }
            var project = await dbContext.Projects.FirstOrDefaultAsync(project => project.Id == id && project.ClientId == clientId, cancellationToken);
            if (project is null)
            {
                return NotFound(new
                {
                    message = "Project not found."
                });
            }

            dbContext.Projects.Remove(project);
            await dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }
    }
}
