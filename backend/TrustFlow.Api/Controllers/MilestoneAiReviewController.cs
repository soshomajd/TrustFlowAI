using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.Controllers;

// The client's AI read of a delivery. Only the client who owns the project
// can ask for it or see it: it is their working note, not a message to the
// freelancer.
[ApiController]
[Authorize(Roles = AppRoles.Client)]
[EnableRateLimiting(RateLimitPolicies.Ai)]
[Route("api/projects/{projectId:guid}/milestones/{milestoneId:guid}/ai-review")]
public sealed class MilestoneAiReviewController(
    AppDbContext dbContext,
    MilestoneReviewService reviews)
    : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId,
        Guid milestoneId,
        [FromQuery] Guid? submissionId,
        CancellationToken cancellationToken)
    {
        if (!await OwnsMilestoneAsync(
                projectId, milestoneId, cancellationToken))
        {
            return NotFound(new { message = "Milestone not found." });
        }

        var review = await reviews.GetExistingAsync(
            milestoneId,
            submissionId,
            cancellationToken);

        return review is null
            ? NotFound(new { message = "No AI review yet." })
            : Ok(review);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid projectId,
        Guid milestoneId,
        [FromQuery] Guid? submissionId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        if (!await OwnsMilestoneAsync(
                projectId, milestoneId, cancellationToken))
        {
            return NotFound(new { message = "Milestone not found." });
        }

        var review = await reviews.ReviewAsync(
            userId,
            projectId,
            milestoneId,
            submissionId,
            cancellationToken);

        return review is null
            ? Conflict(new
            {
                message = "Nothing has been delivered for this milestone."
            })
            : Ok(review);
    }

    private async Task<bool> OwnsMilestoneAsync(
        Guid projectId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return false;
        }

        return await dbContext.Milestones
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id == milestoneId &&
                    item.ProjectId == projectId &&
                    item.Project.ClientId == userId,
                cancellationToken);
    }
}
