using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.Controllers;

// Writing aids. Each one returns a suggestion and changes nothing: the user
// reads it, edits it and saves it through the normal screens.
[ApiController]
[Authorize]
[Route("api/ai")]
[EnableRateLimiting(RateLimitPolicies.Ai)]
public sealed class AiController(
    IAiGateway gateway,
    ProjectDraftService projectDrafts,
    FreelancerCoachService freelancerCoach)
    : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus() =>
        Ok(new AiStatusResponse
        {
            Enabled = gateway.Status.Enabled,
            IsDemo = gateway.Status.IsDemo
        });

    [Authorize(Roles = AppRoles.Client)]
    [HttpPost("project-draft")]
    public async Task<IActionResult> DraftProject(
        ProjectDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        return Ok(await projectDrafts.DraftAsync(
            userId,
            request,
            cancellationToken));
    }

    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPost("freelancer/profile-coach")]
    public async Task<IActionResult> CoachProfile(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        return Ok(await freelancerCoach.CoachAsync(
            userId,
            cancellationToken));
    }

    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPost("freelancer/resume-draft")]
    public async Task<IActionResult> DraftResume(
        ResumeDraftRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        return Ok(await freelancerCoach.DraftResumeAsync(
            userId,
            request.ExtraFacts,
            cancellationToken));
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
