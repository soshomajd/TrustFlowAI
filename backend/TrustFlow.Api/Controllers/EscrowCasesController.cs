using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Models;

namespace TrustFlow.Api.Controllers;

// The cases on a project's escrows, as the client and the freelancers see
// them. Cases are never created here: a side opens one by calling its own
// escrow contract, and the chain sync records it.
[ApiController]
[Authorize]
public sealed class EscrowCasesController(
    AppDbContext dbContext)
    : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/escrow-cases")]
    public async Task<IActionResult> List(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var isClient = await dbContext.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.Id == projectId &&
                    project.ClientId == userId,
                cancellationToken);

        // Anyone else gets only the cases on their own escrow, so a
        // stranger or a freelancer of another stage sees an empty list.
        var rows = await dbContext.EscrowCases
            .AsNoTracking()
            .Where(item =>
                item.Escrow.ProjectId == projectId &&
                (isClient || item.Escrow.FreelancerId == userId))
            .OrderByDescending(item => item.OpenedAt)
            .Take(100)
            .Select(item => new
            {
                Case = item,
                item.Escrow.FreelancerId,
                item.Escrow.ChainId,
                item.Escrow.ContractAddress,
                item.Escrow.ArbiterWalletAddress,
                item.Escrow.PlatformFeeBasisPoints,
                MilestoneTitle = item.Milestone.Title,
                MilestoneAmount = item.Milestone.Amount
            })
            .ToListAsync(cancellationToken);

        return Ok(rows.Select(row => new EscrowCaseResponse
        {
            Id = row.Case.Id,
            EscrowId = row.Case.EscrowId,
            ProjectId = projectId,
            FreelancerId = row.FreelancerId,
            MilestoneId = row.Case.MilestoneId,
            MilestoneTitle = row.MilestoneTitle,
            MilestoneAmount = row.MilestoneAmount,
            Kind = row.Case.Kind.ToString(),
            OpenedByRole = OpenedByRole(row.Case.Kind),
            Status = row.Case.Status.ToString(),
            OpenedAt = row.Case.OpenedAt,
            SettleableAt = row.Case.OpenedAt + EscrowCaseRules.ResponseWindow,
            StallableAt = row.Case.OpenedAt + EscrowCaseRules.StallWindow,
            ClosedAt = row.Case.ClosedAt,
            PaidToFreelancer = row.Case.PaidToFreelancer,
            PlatformFeePaid = row.Case.PlatformFeePaid,
            FreelancerNetPaid = row.Case.PaidToFreelancer - row.Case.PlatformFeePaid,
            PlatformFeeBasisPoints = row.PlatformFeeBasisPoints,
            ReturnedToClient = row.Case.ReturnedToClient,
            EscrowEnded = row.Case.EscrowEnded,
            ReviewNote = row.Case.ReviewNote,
            ChainId = row.ChainId,
            ContractAddress = row.ContractAddress,
            ArbiterWalletAddress = row.ArbiterWalletAddress
        }));
    }

    internal static string OpenedByRole(EscrowCaseKind kind) =>
        kind == EscrowCaseKind.PaymentRequest ? "Freelancer" : "Client";
}
