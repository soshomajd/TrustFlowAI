using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Dtos.Common;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Ai;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/projects/{projectId:guid}/disputes")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDisputesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] AdminDisputeQuery request, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null) return NotFound();
        List<AdminDisputeSummary> items; int total;
        if (request.Kind == "escrow")
        {
            var query = Cases(projectId); total = await query.CountAsync(cancellationToken);
            var rows = await query.OrderByDescending(item => item.OpenedAt).ThenBy(item => item.Id)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
            var names = await Names(rows.Select(item => item.Escrow.FreelancerId).Append(project.ClientId ?? Guid.Empty), cancellationToken);
            items = rows.Select(item => CaseSummary(item, project.ClientId, names)).ToList();
        }
        else
        {
            var query = Written(projectId); total = await query.CountAsync(cancellationToken);
            var rows = await query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
                .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
            var names = await Names(rows.SelectMany(item => new[] { item.OpenedByUserId, item.RespondentUserId }), cancellationToken);
            items = rows.Select(item => WrittenSummary(item, names)).ToList();
        }
        await Audit(projectId, "AdminDisputesListed", null, items.Select(item => item.Id), cancellationToken);
        var pages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Ok(new PagedResponse<AdminDisputeSummary> { Items = items, Page = request.Page, PageSize = request.PageSize, TotalItems = total,
            TotalPages = pages, HasPreviousPage = request.Page > 1, HasNextPage = request.Page < pages });
    }

    [HttpGet("{kind}/{recordId:guid}")]
    public async Task<IActionResult> Get(Guid projectId, string kind, Guid recordId, CancellationToken cancellationToken)
    {
        AdminDisputeDetail detail;
        if (kind == "escrow")
        {
            var row = await Cases(projectId).SingleOrDefaultAsync(item => item.Id == recordId, cancellationToken);
            if (row is null) return NotFound();
            var clientId = await db.Projects.Where(item => item.Id == projectId).Select(item => item.ClientId).SingleAsync(cancellationToken);
            var names = await Names([row.Escrow.FreelancerId, clientId ?? Guid.Empty], cancellationToken);
            // A case and a written dispute share a milestone, but no direct relation is stored.
            // Do not attach the latest statement/opinion from another freelancer's history.
            var fee = row.PaidToFreelancer.HasValue && row.Milestone.PaidAmount == row.PaidToFreelancer
                && row.Milestone.FreelancerId == row.Escrow.FreelancerId ? (decimal?)row.Milestone.PlatformFeePaid : null;
            var outcome = row.Status == EscrowCaseStatus.Settled ? new AdminFinanceSettlement(row.Id, row.MilestoneId, row.Milestone.Title,
                row.PaidToFreelancer, fee, row.ReturnedToClient, row.EscrowEnded, row.ClosedAt) : null;
            detail = new(CaseSummary(row, clientId, names), row.EscrowId, row.Escrow.ChainId, row.Escrow.ContractAddress,
                row.Escrow.TokenAddress, row.Escrow.ArbiterWalletAddress, row.Escrow.ClientWalletAddress, row.Escrow.FreelancerWalletAddress,
                null, null, null, row.ReviewNote, row.ReviewNoteAt, outcome,
                Opinion(row.AdviceJson, row.AdviceFreelancerSharePercent, null, row.AdviceAt, await Demo(row.AdviceAiRunId, cancellationToken)));
        }
        else if (kind == "written")
        {
            var row = await Written(projectId).SingleOrDefaultAsync(item => item.Id == recordId, cancellationToken);
            if (row is null) return NotFound();
            var names = await Names([row.OpenedByUserId, row.RespondentUserId], cancellationToken);
            detail = new(WrittenSummary(row, names), null, null, null, null, null, null, null,
                row.OpenerStatement, row.RespondentStatement, row.RespondentStatementAt, null, null, null,
                Opinion(row.RulingJson, row.FreelancerSharePercent, row.Verdict?.ToString(), row.RuledAt, await Demo(row.AiRunId, cancellationToken)));
        }
        else return NotFound();
        await Audit(projectId, "AdminDisputeViewed", recordId, [], cancellationToken);
        return Ok(detail);
    }

    private IQueryable<EscrowCase> Cases(Guid projectId) => db.EscrowCases.AsNoTracking().Include(item => item.Escrow).Include(item => item.Milestone)
        .Where(item => item.Escrow.ProjectId == projectId && item.Milestone.ProjectId == projectId);
    private IQueryable<Dispute> Written(Guid projectId) => db.Disputes.AsNoTracking().Include(item => item.Milestone)
        .Where(item => item.ProjectId == projectId && item.Milestone.ProjectId == projectId);
    private async Task<Dictionary<Guid, string>> Names(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var selected = ids.Distinct().ToArray();
        return await db.Users.AsNoTracking().Where(item => selected.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FullName, ct);
    }
    private static AdminDisputeSummary CaseSummary(EscrowCase row, Guid? clientId, Dictionary<Guid, string> names) => new(row.Id, "escrow", row.MilestoneId,
        row.Milestone.Title, row.Milestone.Amount, row.Status.ToString(), row.Kind == EscrowCaseKind.PaymentRequest ? "Freelancer" : "Client",
        clientId, names.GetValueOrDefault(clientId ?? Guid.Empty, "Former client"), row.Escrow.FreelancerId,
        names.GetValueOrDefault(row.Escrow.FreelancerId, "Former freelancer"), row.OpenedAt, row.ClosedAt) { CaseKind = row.Kind.ToString() };
    private static AdminDisputeSummary WrittenSummary(Dispute row, Dictionary<Guid, string> names)
    {
        Guid? client = row.OpenedByRole == "Client" ? row.OpenedByUserId : row.OpenedByRole == "Freelancer" ? row.RespondentUserId : null;
        Guid? freelancer = row.OpenedByRole == "Freelancer" ? row.OpenedByUserId : row.OpenedByRole == "Client" ? row.RespondentUserId : null;
        return new(row.Id, "written", row.MilestoneId, row.Milestone.Title, row.Milestone.Amount, row.Status.ToString(), row.OpenedByRole,
            client, names.GetValueOrDefault(client ?? Guid.Empty, "Unrecorded role"), freelancer,
            names.GetValueOrDefault(freelancer ?? Guid.Empty, "Unrecorded role"), row.CreatedAt, row.RuledAt);
    }
    private async Task<bool?> Demo(Guid? runId, CancellationToken ct)
    {
        if (runId is null) return null;
        var provider = await db.AiRuns.Where(item => item.Id == runId).Select(item => item.Provider).SingleOrDefaultAsync(ct);
        return string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase) ? true
            : string.Equals(provider, "Anthropic", StringComparison.OrdinalIgnoreCase) ? false : null;
    }
    private static AdminSavedOpinion? Opinion(string? json, int? share, string? verdict, DateTimeOffset? time, bool? demo)
    {
        if (json is null) return share.HasValue || verdict is not null || time.HasValue
            ? new(false, null, null, share is >= 0 and <= 100 ? share : null, verdict, time, demo) : null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return new(false, null, null, null, verdict, time, demo);
            string? Text(string key) => root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return new(true, Text("summary"), Text("reasoning"), share is >= 0 and <= 100 ? share : null, verdict, time, demo);
        }
        catch (JsonException) { return new(false, null, null, null, verdict, time, demo); }
    }
    private async Task Audit(Guid projectId, string action, Guid? id, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = id, Action = action });
        db.AdminAccessLogs.AddRange(ids.Select(resource => new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = resource, Action = action + "Record" }));
        await db.SaveChangesAsync(ct); Response.Headers.CacheControl = "private, no-store";
    }
}
