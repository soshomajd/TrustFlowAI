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



    [Authorize(Roles = AppRoles.Client)]
    [HttpPost]
    public async Task<IActionResult> CreateMilestone(
        Guid projectId,
        CreateMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }



        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                project => project.Id == projectId && project.ClientId == clientId,
                cancellationToken
            );

        if (project is null)
        {
            return NotFound(new
            {
                message = "Project not found."
            });
        }

        var sequenceExists = await dbContext.Milestones
            .AnyAsync(
                milestone =>
                    milestone.ProjectId == projectId &&
                    milestone.SequenceNumber == request.SequenceNumber,
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
            .Where(milestone => milestone.ProjectId == projectId)
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
                remainingBudget = project.Budget - allocatedAmount
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
            Deadline = request.Deadline
        };

        dbContext.Milestones.Add(milestone);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/projects/{projectId}/milestones/{milestone.Id}",
            milestone
        );

    }
    [HttpGet]
    public async Task<IActionResult> GetMilestones(
    Guid projectId,
    CancellationToken cancellationToken)
    {
        var projectExists = await dbContext.Projects
            .AnyAsync(
                project => project.Id == projectId,
                cancellationToken
            );

        if (!projectExists)
        {
            return NotFound(new
            {
                message = "Project not found."
            });
        }

        var milestones = await dbContext.Milestones
            .AsNoTracking()
            .Where(milestone => milestone.ProjectId == projectId)
            .OrderBy(milestone => milestone.SequenceNumber)
            .ToListAsync(cancellationToken);

        return Ok(milestones);
    }
    [HttpGet("{milestoneId:guid}")]
    public async Task<IActionResult> GetMilestoneById(
    Guid projectId,
    Guid milestoneId,
    CancellationToken cancellationToken)
    {
        var milestone = await dbContext.Milestones
            .AsNoTracking()
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
        var clientIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var project = await dbContext.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(
                project => project.Id == projectId && project.ClientId == clientId,
                cancellationToken
            );

        if (project is null)
        {
            return NotFound(new
            {
                message = "Project not found."
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

        var sequenceExists = await dbContext.Milestones
            .AnyAsync(
                otherMilestone =>
                    otherMilestone.ProjectId == projectId &&
                    otherMilestone.SequenceNumber == request.SequenceNumber &&
                    otherMilestone.Id != milestoneId,
                cancellationToken
            );

        if (sequenceExists)
        {
            return Conflict(new
            {
                message = "This sequence number already exists."
            });
        }

        var otherMilestonesAmount = await dbContext.Milestones
            .Where(otherMilestone =>
                otherMilestone.ProjectId == projectId &&
                otherMilestone.Id != milestoneId)
            .SumAsync(
                otherMilestone => (decimal?)otherMilestone.Amount,
                cancellationToken
            ) ?? 0m;

        if (otherMilestonesAmount + request.Amount > project.Budget)
        {
            return BadRequest(new
            {
                message = "Milestone amounts cannot exceed project budget.",
                projectBudget = project.Budget,
                allocatedAmount = otherMilestonesAmount,
                requestedAmount = request.Amount
            });
        }

        if (request.Deadline > project.Deadline)
        {
            return BadRequest(new
            {
                message = "Milestone deadline cannot be after project deadline."
            });
        }

        milestone.Title = request.Title;
        milestone.Description = request.Description;
        milestone.Amount = request.Amount;
        milestone.SequenceNumber = request.SequenceNumber;
        milestone.Deadline = request.Deadline;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(milestone);
    }
    [HttpDelete("{milestoneId:guid}")]
    [Authorize(Roles = AppRoles.Client)]
    public async Task<IActionResult> DeleteMilestone(
    Guid projectId,
    Guid milestoneId,
    CancellationToken cancellationToken)
    {
        var clientIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(clientIdValue, out var clientId))
        {
            return Unauthorized();
        }

        var milestone = await dbContext.Milestones
            .FirstOrDefaultAsync(
                milestone =>
                    milestone.Id == milestoneId &&
                    milestone.ProjectId == projectId && milestone.Project.ClientId == clientId,
                cancellationToken
            );

        if (milestone is null)
        {
            return NotFound(new
            {
                message = "Milestone not found."
            });
        }

        dbContext.Milestones.Remove(milestone);

        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}