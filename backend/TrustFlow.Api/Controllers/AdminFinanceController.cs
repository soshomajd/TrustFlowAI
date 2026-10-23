using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Dtos.Common;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/projects/{projectId:guid}/finance")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminFinanceController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] AdminPageQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == projectId, cancellationToken)) return NotFound();
        var query = Escrows(projectId);
        return await PageAsync(projectId, "AdminFinanceViewed", query, request, item => item.Id, cancellationToken);
    }

    [HttpGet("{escrowId:guid}/{section}")]
    public async Task<IActionResult> Entries(Guid projectId, Guid escrowId, string section,
        [FromQuery] AdminPageQuery request, CancellationToken cancellationToken)
    {
        if (section is not ("payouts" or "settlements")) return NotFound();
        var escrow = await db.Escrows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == escrowId && item.ProjectId == projectId, cancellationToken);
        if (escrow is null) return NotFound();
        return section == "payouts"
            ? await PageAsync(projectId, "AdminFinancePayoutsViewed", Payouts(projectId, escrow), request, item => item.Id, cancellationToken)
            : await PageAsync(projectId, "AdminFinanceSettlementsViewed", Settlements(escrow), request, item => item.Id, cancellationToken);
    }

    [HttpGet("{escrowId:guid}/report.csv")]
    public async Task<IActionResult> Report(Guid projectId, Guid escrowId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var projectTitle = await db.Projects.AsNoTracking().Where(item => item.Id == projectId).Select(item => item.Title).SingleOrDefaultAsync(cancellationToken);
        var escrow = await db.Escrows.AsNoTracking().SingleOrDefaultAsync(item => item.Id == escrowId && item.ProjectId == projectId, cancellationToken);
        if (projectTitle is null || escrow is null) return NotFound();
        // Refuse an oversized report instead of silently exporting a partial history.
        const int limit = 10000;
        var payouts = await Payouts(projectId, escrow).Take(limit + 1).ToListAsync(cancellationToken);
        var settlements = await Settlements(escrow).Take(limit + 1).ToListAsync(cancellationToken);
        if (payouts.Count > limit || settlements.Count > limit)
            return Problem(statusCode: 409, title: "Report is too large", detail: "This report exceeds 10,000 records per section. Use the paginated financial history.");
        var summary = await Escrows(projectId).SingleAsync(item => item.Id == escrowId, cancellationToken);
        var bytes = TrustFlow.Api.Services.Admin.AdminFinanceCsv.Create(projectId, projectTitle, summary, payouts, settlements, DateTimeOffset.UtcNow);
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = escrowId, Action = "AdminFinanceReportExported" });
        db.AdminAccessLogs.AddRange(payouts.Select(item => item.Id).Concat(settlements.Select(item => item.Id))
            .Select(id => new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = id, Action = "AdminFinanceReportExportedRecord" }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(bytes, "text/csv; charset=utf-8", $"trustflow-finance-{projectId}-{escrowId}.csv");
    }

    private IQueryable<AdminFinanceEscrow> Escrows(Guid projectId) =>
        db.Escrows.AsNoTracking().Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new AdminFinanceEscrow
            {
                Id = item.Id, FreelancerId = item.FreelancerId,
                FreelancerName = db.Users.Where(user => user.Id == item.FreelancerId).Select(user => user.FullName).FirstOrDefault() ?? "Former freelancer",
                Status = item.Status, ChainId = item.ChainId, TokenAddress = item.TokenAddress,
                ContractAddress = item.ContractAddress, TokenDecimals = item.TokenDecimals, TotalAmount = item.TotalAmount,
                GrossReleased = item.ReleasedAmount, PlatformFeesPaid = item.PlatformFeesPaid,
                DeploymentTransactionHash = item.DeploymentTransactionHash, FundingTransactionHash = item.FundingTransactionHash,
                CancellationTransactionHash = item.CancellationTransactionHash, FundedAt = item.FundedAt,
                CancelledAt = item.CancelledAt, UpdatedAt = item.UpdatedAt,
                WorkStartedAt = db.EscrowExecutionRecords.Where(r => r.EscrowId == item.Id && r.Kind == "WorkStarted").Select(r => (DateTimeOffset?)r.OccurredAt).FirstOrDefault(),
                WorkStartTransactionHash = db.EscrowExecutionRecords.Where(r => r.EscrowId == item.Id && r.Kind == "WorkStarted").Select(r => r.TransactionHash).FirstOrDefault(),
                RecordedCaseRefunds = db.EscrowCases.Where(c => c.EscrowId == item.Id && c.Status == EscrowCaseStatus.Settled).Sum(c => c.ReturnedToClient) ?? 0,
                UnknownCaseRefundCount = db.EscrowCases.Count(c => c.EscrowId == item.Id && c.Status == EscrowCaseStatus.Settled && c.ReturnedToClient == null),
                RecordedMilestoneGross = db.Milestones.Where(m => m.ProjectId == projectId && m.FreelancerId == item.FreelancerId
                    && (m.Status == MileStoneStatus.Approved || m.Status == MileStoneStatus.Settled)).Sum(m => m.PaidAmount) ?? 0,
                RecordedMilestoneFees = db.Milestones.Where(m => m.ProjectId == projectId && m.FreelancerId == item.FreelancerId
                    && (m.Status == MileStoneStatus.Approved || m.Status == MileStoneStatus.Settled)).Sum(m => (decimal?)m.PlatformFeePaid) ?? 0,
                UnknownPayoutAmountCount = db.Milestones.Count(m => m.ProjectId == projectId && m.FreelancerId == item.FreelancerId
                    && (m.Status == MileStoneStatus.Approved || m.Status == MileStoneStatus.Settled) && m.PaidAmount == null)
            });

    private IQueryable<AdminFinancePayout> Payouts(Guid projectId, Escrow escrow) => db.Milestones.AsNoTracking()
        .Where(item => item.ProjectId == projectId && item.FreelancerId == escrow.FreelancerId
            && (item.Status == MileStoneStatus.Approved || item.Status == MileStoneStatus.Settled))
        .OrderBy(item => item.SequenceNumber).ThenBy(item => item.Id)
        .Select(item => new AdminFinancePayout(item.Id, item.Title, item.Status, item.PaidAmount, item.PlatformFeePaid,
            db.EscrowCases.Where(c => c.EscrowId == escrow.Id && c.MilestoneId == item.Id && c.Status == EscrowCaseStatus.Settled)
                .OrderBy(c => c.Id).Select(c => (Guid?)c.Id).FirstOrDefault(),
            db.EscrowCases.Where(c => c.EscrowId == escrow.Id && c.MilestoneId == item.Id && c.Status == EscrowCaseStatus.Settled)
                .OrderBy(c => c.Id).Select(c => c.ClosedAt).FirstOrDefault(),
            db.EscrowExecutionRecords.Where(r => r.EscrowId == escrow.Id && r.MilestoneId == item.Id).Select(r => r.TransactionHash).FirstOrDefault(),
            db.EscrowExecutionRecords.Where(r => r.EscrowId == escrow.Id && r.MilestoneId == item.Id).Select(r => (DateTimeOffset?)r.OccurredAt).FirstOrDefault()));

    private IQueryable<AdminFinanceSettlement> Settlements(Escrow escrow) => db.EscrowCases.AsNoTracking()
        .Where(item => item.EscrowId == escrow.Id && item.Status == EscrowCaseStatus.Settled)
        .OrderByDescending(item => item.ClosedAt).ThenBy(item => item.Id)
        .Select(item => new AdminFinanceSettlement(item.Id, item.MilestoneId, item.Milestone.Title, item.PaidToFreelancer,
            item.PaidToFreelancer.HasValue && item.Milestone.PaidAmount.HasValue
                && item.Milestone.PaidAmount == item.PaidToFreelancer && item.Milestone.FreelancerId == escrow.FreelancerId
                ? (decimal?)item.Milestone.PlatformFeePaid : null,
            item.ReturnedToClient, item.EscrowEnded, item.ClosedAt,
            db.EscrowExecutionRecords.Where(r => r.EscrowId == escrow.Id && r.MilestoneId == item.MilestoneId && r.Kind == "CaseSettlement").Select(r => r.TransactionHash).FirstOrDefault(),
            db.EscrowExecutionRecords.Where(r => r.EscrowId == escrow.Id && r.MilestoneId == item.MilestoneId && r.Kind == "CaseSettlement").Select(r => (DateTimeOffset?)r.OccurredAt).FirstOrDefault()));

    private async Task<IActionResult> PageAsync<T>(Guid projectId, string action, IQueryable<T> query,
        AdminPageQuery request, Func<T, Guid> id, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = actor, ProjectId = projectId, Action = action });
        db.AdminAccessLogs.AddRange(items.Select(item => new AdminAccessLog { AdminId = actor, ProjectId = projectId,
            ResourceId = id(item), Action = action + "Record" }));
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        var pages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Ok(new PagedResponse<T> { Items = items, Page = request.Page, PageSize = request.PageSize, TotalItems = total,
            TotalPages = pages, HasPreviousPage = request.Page > 1, HasNextPage = request.Page < pages });
    }
}
