using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.Controllers;

// Disputes over a delivered milestone. There is deliberately no moderator,
// admin or support route here: the only people who can open, read or answer
// a dispute are the client and the freelancer on that milestone, and
// nobody, including them, can edit or delete a ruling.
[ApiController]
[Authorize]
public sealed class DisputesController(DisputeService disputes)
    : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/milestones/{milestoneId:guid}/disputes")]
    public async Task<IActionResult> ListForMilestone(
        Guid projectId,
        Guid milestoneId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        return Ok(await disputes.ListForMilestoneAsync(
            userId,
            projectId,
            milestoneId,
            cancellationToken));
    }

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("api/projects/{projectId:guid}/milestones/{milestoneId:guid}/disputes")]
    public async Task<IActionResult> Open(
        Guid projectId,
        Guid milestoneId,
        OpenDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var outcome = await disputes.OpenAsync(
            userId,
            projectId,
            milestoneId,
            request.Statement,
            cancellationToken);

        return outcome.Dispute is null
            ? Fail(outcome)
            : Created(
                $"/api/disputes/{outcome.Dispute.Id}",
                outcome.Dispute);
    }

    [HttpGet("api/disputes/{disputeId:guid}")]
    public async Task<IActionResult> Get(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var outcome = await disputes.GetAsync(
            userId,
            disputeId,
            cancellationToken);

        return outcome.Dispute is null ? Fail(outcome) : Ok(outcome.Dispute);
    }

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("api/disputes/{disputeId:guid}/response")]
    public async Task<IActionResult> Respond(
        Guid disputeId,
        RespondToDisputeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var outcome = await disputes.RespondAsync(
            userId,
            disputeId,
            request.Statement,
            cancellationToken);

        return outcome.Dispute is null ? Fail(outcome) : Ok(outcome.Dispute);
    }

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("api/disputes/{disputeId:guid}/ruling")]
    public async Task<IActionResult> RequestRuling(
        Guid disputeId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var outcome = await disputes.RequestRulingAsync(
            userId,
            disputeId,
            cancellationToken);

        return outcome.Dispute is null ? Fail(outcome) : Ok(outcome.Dispute);
    }

    private IActionResult Fail(DisputeOutcome outcome) =>
        outcome.Failure == DisputeFailure.NotFound
            ? NotFound(new { message = outcome.Message })
            : Conflict(new { message = outcome.Message });

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
