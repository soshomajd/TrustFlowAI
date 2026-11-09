using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController, Authorize]
[Route("api/projects/{projectId:guid}/escrow/stages")]
public sealed class EscrowFundingStagesController(AppDbContext db, IEscrowChainSyncOrchestrator sync,
    IEscrowChainDeployer chain) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStages(Guid projectId, [FromQuery] Guid freelancerId, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var escrow = await db.Escrows.AsNoTracking().SingleOrDefaultAsync(e => e.ProjectId == projectId &&
            e.FreelancerId == freelancerId && (e.Project.ClientId == userId || e.FreelancerId == userId), ct);
        if (escrow is null) return NotFound();
        var stages = await db.EscrowFundingStages.AsNoTracking().Where(s => s.EscrowId == escrow.Id)
            .OrderBy(s => s.PlanOrder).Select(s => new {
                s.Id, s.MilestoneId, title = s.Title, s.ChainIndex, s.Amount,
                s.FundingReference, s.TermsHash, s.FundingTransactionHash, s.FundedAt,
                s.StartTransactionHash, s.StartedAt, s.ReservationActive
            }).ToListAsync(ct);
        return Ok(stages.Select(s => new { s.Id, s.MilestoneId, s.title, s.ChainIndex, s.Amount,
            amountExact = s.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            s.FundingReference, s.TermsHash, s.FundingTransactionHash, s.FundedAt, s.StartTransactionHash, s.StartedAt, s.ReservationActive }));
    }

    public sealed record PrepareStageRequest(Guid MilestoneId, Guid FreelancerId,
        string? Title = null, string? Description = null, decimal? Amount = null, DateTimeOffset? Deadline = null);

    [HttpPost, Authorize(Roles = AppRoles.Client)]
    public async Task<IActionResult> PrepareNext(Guid projectId, PrepareStageRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var clientId)) return Unauthorized();
        var escrow = await db.Escrows.AsNoTracking().SingleOrDefaultAsync(e => e.ProjectId == projectId &&
            e.FreelancerId == request.FreelancerId && e.Project.ClientId == clientId && e.ProtocolVersion == 3, ct);
        if (escrow?.ContractAddress is null) return NotFound();
        try
        {
            await sync.TrySyncAsync(escrow.Id, ct);
            var snapshot = await chain.GetEscrowSnapshotAsync(escrow.ContractAddress, ct);
            if (snapshot.State != OnChainEscrowState.Completed)
                return Conflict(new { message = "Finish the funded milestone before funding the next one." });
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var all = await db.EscrowFundingStages.Where(s => s.EscrowId == escrow.Id).OrderBy(s => s.PlanOrder).ToListAsync(ct);
            var stage = all.SingleOrDefault(s => s.MilestoneId == request.MilestoneId);
            var pending = all.SingleOrDefault(s => s.ChainIndex == snapshot.NextMilestoneIndex);
            if (pending != null)
            {
                if (request.MilestoneId != Guid.Empty && pending.MilestoneId != request.MilestoneId)
                    return Conflict(new { message = "Resume the already prepared milestone first." });
                return Ok(new { pending.Id, pending.MilestoneId });
            }
            var requestedId = request.MilestoneId;
            if (requestedId == Guid.Empty)
            {
                var project = await db.Projects.SingleAsync(p => p.Id == projectId, ct);
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 ||
                    (request.Description?.Length ?? 0) > 5000 || request.Amount is not > 0 || request.Amount > 1_000_000_000m ||
                    decimal.Round(request.Amount.Value, 2) != request.Amount.Value || request.Deadline == null ||
                    request.Deadline <= DateTimeOffset.UtcNow || request.Deadline > project.Deadline)
                    return BadRequest(new { message = "Enter a title, a positive amount with up to two decimal places, and a future deadline within the project deadline." });
                if (all.Any(s => s.ChainIndex == null && s.ReservationActive))
                    return Conflict(new { message = "Fund the reserved milestone before adding another one." });
                var added = new MileStone { ProjectId = projectId, Title = request.Title.Trim(), Description = request.Description?.Trim() ?? "",
                    Amount = request.Amount.Value, Deadline = request.Deadline.Value.ToUniversalTime(),
                    SequenceNumber = await db.Milestones.Where(m => m.ProjectId == projectId).MaxAsync(m => m.SequenceNumber, ct) + 1 };
                db.Milestones.Add(added);
                project.Budget += added.Amount;
                if (project.Status == ProjectStatus.Completed) project.Status = ProjectStatus.Open;
                await db.SaveChangesAsync(ct);
                requestedId = added.Id;
            }
            var milestone = await db.Milestones.SingleOrDefaultAsync(m => m.Id == requestedId && m.ProjectId == projectId, ct);
            if (milestone == null) return NotFound();
            if (milestone.Status != MileStoneStatus.Pending || milestone.FreelancerId != null ||
                await db.EscrowFundingStages.AnyAsync(s => s.MilestoneId == request.MilestoneId && s.ReservationActive && s.EscrowId != escrow.Id, ct))
                return Conflict(new { message = "This milestone is unavailable for new funding." });
            var nextPlanned = all.FirstOrDefault(s => s.ChainIndex == null && s.ReservationActive);
            if (nextPlanned != null && nextPlanned.MilestoneId != request.MilestoneId)
                return Conflict(new { message = "Fund the reserved milestones in their agreed order." });
            if (stage == null)
            {
                stage = FundedHiringPreparation.NewStage(escrow, milestone, all.Count, snapshot.NextMilestoneIndex);
                db.EscrowFundingStages.Add(stage);
            }
            else stage.ChainIndex = snapshot.NextMilestoneIndex;
            milestone.FreelancerId = escrow.FreelancerId;
            milestone.OnChainIndex = stage.ChainIndex;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(new { stage.Id, stage.MilestoneId });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex) when (ex is DbUpdateException || ex is PostgresException { SqlState: "40001" })
        { return Conflict(new { message = "The funding plan changed. Refresh and resume the reserved stage." }); }
    }
}
