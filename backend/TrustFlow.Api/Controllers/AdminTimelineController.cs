using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/projects/{projectId:guid}/timeline")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminTimelineController(AppDbContext db, IDataProtectionProvider protection) : ControllerBase
{
    private sealed record Position(Guid ProjectId, Guid AdminId, string Category, string Timing,
        DateTimeOffset? From, DateTimeOffset? To, Guid? ActorId, DateTimeOffset SnapshotAt,
        DateTimeOffset? At, int Code, Guid Id);

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, [FromQuery] AdminTimelineQuery request, CancellationToken cancellationToken)
    {
        if (request.From > request.To || (request.Timing == "missing" && (request.From.HasValue || request.To.HasValue)))
            return BadRequest(new { message = "Choose a valid recorded-time range. Undated records cannot be filtered by date." });
        if (!await db.Projects.AnyAsync(item => item.Id == projectId, cancellationToken)) return NotFound();
        var admin = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var protector = protection.CreateProtector("TrustFlow.AdminTimeline.v1");
        Position? cursor = null;
        if (request.Cursor is not null)
        {
            try { cursor = JsonSerializer.Deserialize<Position>(protector.Unprotect(request.Cursor)); }
            catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
            { return BadRequest(new { message = "This history cursor is invalid. Refresh latest events." }); }
            if (cursor is null || cursor.ProjectId != projectId || cursor.AdminId != admin || cursor.Category != request.Category ||
                cursor.Timing != request.Timing || cursor.From != request.From || cursor.To != request.To || cursor.ActorId != request.ActorId ||
                cursor.SnapshotAt < DateTimeOffset.UtcNow.AddHours(-2) || cursor.SnapshotAt > DateTimeOffset.UtcNow)
                return BadRequest(new { message = "This history cursor belongs to another view or has expired. Refresh latest events." });
        }
        var snapshot = cursor?.SnapshotAt ?? DateTimeOffset.UtcNow;
        var from = request.From?.ToUniversalTime();
        var to = request.To?.ToUniversalTime();
        var candidates = new List<AdminTimelineEvent>();
        // Each source is bounded in SQL before merging. No message/file bodies are materialized.
        async Task Add(string category, IQueryable<AdminTimelineEvent> query)
        {
            if (request.Category != "all" && request.Category != category) return;
            // Access logs have their own filter so frequent admin reads cannot bury workflow events.
            if (category == "admin" && request.Category == "all") return;
            query = request.Timing == "missing" ? query.Where(item => item.OccurredAt == null) :
                query.Where(item => item.OccurredAt != null && item.OccurredAt <= snapshot);
            if (from.HasValue) query = query.Where(item => item.OccurredAt >= from);
            if (to.HasValue) query = query.Where(item => item.OccurredAt <= to);
            if (request.ActorId.HasValue) query = query.Where(item => item.ActorId == request.ActorId);
            if (cursor is not null)
            {
                var at = cursor.At; var code = cursor.Code; var id = cursor.Id;
                query = request.Timing == "missing"
                    ? query.Where(item => item.Code > code || (item.Code == code && item.ResourceId.CompareTo(id) > 0))
                    : query.Where(item => item.OccurredAt < at || (item.OccurredAt == at &&
                        (item.Code > code || (item.Code == code && item.ResourceId.CompareTo(id) > 0))));
            }
            candidates.AddRange(await query.OrderByDescending(item => item.OccurredAt).ThenBy(item => item.Code).ThenBy(item => item.ResourceId)
                .Take(request.PageSize + 1).ToListAsync(cancellationToken));
        }

        var projects = db.Projects.AsNoTracking().Where(item => item.Id == projectId && item.CreatedAt <= snapshot);
        var milestones = db.Milestones.AsNoTracking().Where(item => item.ProjectId == projectId && item.CreatedAt <= snapshot);
        var proposals = db.Proposals.AsNoTracking().Where(item => item.ProjectId == projectId && item.CreatedAt <= snapshot);
        var escrows = db.Escrows.AsNoTracking().Where(item => item.ProjectId == projectId && item.CreatedAt <= snapshot);
        var deliveries = db.MilestoneSubmissions.AsNoTracking().Where(item => item.Milestone.ProjectId == projectId && item.SubmittedAt <= snapshot);
        var threads = db.ChatThreads.AsNoTracking().Where(item => item.ProjectId == projectId && item.CreatedAt <= snapshot);
        var cases = db.EscrowCases.AsNoTracking().Where(item => item.Escrow.ProjectId == projectId && item.Milestone.ProjectId == projectId && item.CreatedAt <= snapshot);
        var disputes = db.Disputes.AsNoTracking().Where(item => item.ProjectId == projectId && item.Milestone.ProjectId == projectId && item.CreatedAt <= snapshot);

        await Add("project", projects.Select(item => new AdminTimelineEvent { Code = 1, Category = "project", ResourceId = item.Id,
            Title = "Project created", Label = item.Title, OccurredAt = item.CreatedAt, ActorId = null }));
        await Add("milestones", milestones.Select(item => new AdminTimelineEvent { Code = 10, Category = "milestones", ResourceId = item.Id,
            Title = "Milestone created", Label = item.Title, OccurredAt = item.CreatedAt, ActorId = null }));
        await Add("proposals", proposals.Select(item => new AdminTimelineEvent { Code = 20, Category = "proposals", ResourceId = item.Id,
            Title = "Proposal submitted", OccurredAt = item.CreatedAt, ActorId = item.FreelancerId }));
        await Add("proposals", proposals.Where(item => item.Status != ProposalStatus.Pending).Select(item => new AdminTimelineEvent {
            Code = 21, Category = "proposals", ResourceId = item.Id, Title = "Proposal status changed — time not recorded", OccurredAt = null, ActorId = null }));

        await Add("escrows", escrows.Select(item => new AdminTimelineEvent { Code = 30, Category = "escrows", ResourceId = item.Id,
            Title = "Escrow record created", OccurredAt = item.CreatedAt, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.DeployedAt != null || item.ContractAddress != null).Select(item => new AdminTimelineEvent {
            Code = 31, Category = "escrows", ResourceId = item.Id, Title = "Deployment recorded", OccurredAt = item.DeployedAt, TransactionHash = item.DeploymentTransactionHash, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.FundedAt != null || item.Status == EscrowStatus.Funded || item.Status == EscrowStatus.Completed || item.Status == EscrowStatus.Terminated)
            .Select(item => new AdminTimelineEvent { Code = 32, Category = "escrows", ResourceId = item.Id,
                Title = "Funding recorded", OccurredAt = item.FundedAt, TransactionHash = item.FundingTransactionHash, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.CompletedAt != null || item.Status == EscrowStatus.Completed).Select(item => new AdminTimelineEvent {
            Code = 33, Category = "escrows", ResourceId = item.Id, Title = "Escrow completion recorded", OccurredAt = item.CompletedAt, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.CancelledAt != null || item.Status == EscrowStatus.Cancelled).Select(item => new AdminTimelineEvent {
            Code = 34, Category = "escrows", ResourceId = item.Id, Title = "Cancellation recorded", OccurredAt = item.CancelledAt, TransactionHash = item.CancellationTransactionHash, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.Status == EscrowStatus.Terminated).Select(item => new AdminTimelineEvent {
            Code = 35, Category = "escrows", ResourceId = item.Id, Title = "Escrow termination — time not recorded", OccurredAt = null, ActorId = null }));
        await Add("escrows", escrows.Where(item => item.CancellationNoticeReadAt != null).Select(item => new AdminTimelineEvent {
            Code = 36, Category = "escrows", ResourceId = item.Id, Title = "Cancellation notice read", OccurredAt = item.CancellationNoticeReadAt, ActorId = item.FreelancerId }));
        // PaidAmount is an outcome, not evidence of a release time, release transaction or historical actor.
        await Add("milestones", milestones.Where(item => item.PaidAmount != null && !db.EscrowExecutionRecords.Any(r => r.MilestoneId == item.Id && r.Escrow.ProjectId == projectId)).Select(item => new AdminTimelineEvent {
            Code = 11, Category = "milestones", ResourceId = item.Id, Label = item.Title, Title = "Milestone payout — time and transaction not recorded", OccurredAt = null, ActorId = null }));
        await Add("milestones", milestones.Where(item => item.Status != MileStoneStatus.Pending).Select(item => new AdminTimelineEvent {
            Code = 12, Category = "milestones", ResourceId = item.Id, Label = item.Title, Title = "Milestone status changed — transition time not recorded", OccurredAt = null, ActorId = null }));

        await Add("deliveries", deliveries.Select(item => new AdminTimelineEvent { Code = 40, Category = "deliveries", ResourceId = item.Id,
            RelatedId = item.MilestoneId, Label = item.Milestone.Title, Title = "Delivery version submitted", OccurredAt = item.SubmittedAt, ActorId = item.FreelancerId }));
        await Add("deliveries", deliveries.Where(item => item.ReviewedAt != null || item.ClientFeedback != null).Select(item => new AdminTimelineEvent {
            Code = 41, Category = "deliveries", ResourceId = item.Id, RelatedId = item.MilestoneId, Label = item.Milestone.Title,
            Title = "Delivery review recorded", OccurredAt = item.ReviewedAt, ActorId = null }));
        // Attachments are saved with a version; no separate upload timestamp exists.
        await Add("deliveries", db.MilestoneSubmissionAttachments.AsNoTracking().Where(item => item.Submission.Milestone.ProjectId == projectId && item.Submission.SubmittedAt <= snapshot)
            .Select(item => new AdminTimelineEvent { Code = 42, Category = "deliveries", ResourceId = item.Id, RelatedId = item.SubmissionId,
                Label = item.OriginalFileName, Title = "File attached to delivery version — upload time not recorded", ActorId = item.Submission.FreelancerId, OccurredAt = null }));
        await Add("deliveries", db.MilestoneAiReviews.AsNoTracking().Where(item => item.Submission.Milestone.ProjectId == projectId)
            .Select(item => new AdminTimelineEvent { Code = 43, Category = "deliveries", ResourceId = item.SubmissionId,
                Title = "AI delivery advice saved (not a decision)", OccurredAt = item.CreatedAt, ActorId = null }));
        await Add("conversations", threads.Select(item => new AdminTimelineEvent { Code = 50, Category = "conversations", ResourceId = item.Id,
            Title = "Conversation created", OccurredAt = item.CreatedAt, ActorId = null }));
        await Add("conversations", db.ChatMessages.AsNoTracking().Where(item => item.Thread.ProjectId == projectId)
            .Select(item => new AdminTimelineEvent { Code = 51, Category = "conversations", ResourceId = item.Id, RelatedId = item.ThreadId,
                Title = "Message sent", OccurredAt = item.SentAt, ActorId = item.SenderId }));

        await Add("disputes", cases.Select(item => new AdminTimelineEvent { Code = 60, Category = "disputes", ResourceId = item.Id, RelatedId = item.EscrowId,
            Title = "Financial case opened (chain time)", OccurredAt = item.OpenedAt, ActorId = null }));
        await Add("disputes", cases.Where(item => item.Status != EscrowCaseStatus.Open || item.ClosedAt != null).Select(item => new AdminTimelineEvent {
            Code = 61, Category = "disputes", ResourceId = item.Id, RelatedId = item.EscrowId, Title = "Financial case closed", OccurredAt = item.ClosedAt, ActorId = null }));
        await Add("disputes", cases.Where(item => item.ReviewNoteAt != null || item.ReviewNote != null).Select(item => new AdminTimelineEvent {
            Code = 62, Category = "disputes", ResourceId = item.Id, RelatedId = item.EscrowId, Title = "Reviewer note recorded", OccurredAt = item.ReviewNoteAt, ActorId = null }));
        await Add("disputes", cases.Where(item => item.AdviceAt != null || item.AdviceJson != null).Select(item => new AdminTimelineEvent {
            Code = 63, Category = "disputes", ResourceId = item.Id, RelatedId = item.EscrowId, Title = "AI case advice saved (not a decision)", OccurredAt = item.AdviceAt, ActorId = null }));
        await Add("disputes", disputes.Select(item => new AdminTimelineEvent { Code = 70, Category = "disputes", ResourceId = item.Id, RelatedId = item.MilestoneId,
            Title = "Written disagreement opened", OccurredAt = item.CreatedAt, ActorId = item.OpenedByUserId }));
        await Add("disputes", disputes.Where(item => item.RespondentStatementAt != null || item.RespondentStatement != null).Select(item => new AdminTimelineEvent {
            Code = 71, Category = "disputes", ResourceId = item.Id, RelatedId = item.MilestoneId, Title = "Disagreement response saved",
            OccurredAt = item.RespondentStatementAt, ActorId = item.RespondentUserId }));
        await Add("disputes", disputes.Where(item => item.RuledAt != null || item.RulingJson != null).Select(item => new AdminTimelineEvent {
            Code = 72, Category = "disputes", ResourceId = item.Id, RelatedId = item.MilestoneId, Title = "AI disagreement opinion saved (not a payment)", OccurredAt = item.RuledAt, ActorId = null }));
        await Add("admin", db.AdminAccessLogs.AsNoTracking().Where(item => item.ProjectId == projectId && item.CreatedAt < snapshot)
            .Select(item => new AdminTimelineEvent { Code = 80, Category = "admin", ResourceId = item.Id, RelatedId = item.ResourceId,
                Title = "Administrator access recorded", Label = item.Action, OccurredAt = item.CreatedAt, ActorId = item.AdminId }));

        var executions = db.EscrowExecutionRecords.AsNoTracking().Where(item => item.Escrow.ProjectId == projectId &&
            (item.MilestoneId == null || db.Milestones.Any(m => m.Id == item.MilestoneId && m.ProjectId == projectId)));
        await Add("escrows", executions.Where(item => item.Kind == "WorkStarted").Select(item => new AdminTimelineEvent {
            Code = 37, Category = "escrows", ResourceId = item.Id, RelatedId = item.EscrowId, Title = "Work started (chain time)",
            OccurredAt = item.OccurredAt, ActorId = null, TransactionHash = item.TransactionHash,
            TransactionSender = item.TransactionSender, BlockNumber = item.BlockNumber }));
        await Add("milestones", executions.Where(item => item.Kind == "ClientRelease" || item.Kind == "CaseSettlement").Select(item => new AdminTimelineEvent {
            Code = 13, Category = "milestones", ResourceId = item.Id, RelatedId = item.MilestoneId,
            Title = item.Kind == "ClientRelease" ? "Milestone payment released (chain time)" : "Milestone case settled (chain time)",
            OccurredAt = item.OccurredAt, ActorId = null, TransactionHash = item.TransactionHash,
            TransactionSender = item.TransactionSender, BlockNumber = item.BlockNumber }));

        var ordered = candidates.OrderByDescending(item => item.OccurredAt).ThenBy(item => item.Code).ThenBy(item => item.ResourceId).ToList();
        var page = ordered.Take(request.PageSize).ToList();
        var actorIds = page.Where(item => item.ActorId.HasValue).Select(item => item.ActorId!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(item => actorIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.FullName, cancellationToken);
        page = page.Select(item => item with { ActorName = item.ActorId.HasValue ? names.GetValueOrDefault(item.ActorId.Value, "Former account") : null }).ToList();
        string? next = null;
        if (ordered.Count > request.PageSize)
        {
            var last = page[^1];
            next = protector.Protect(JsonSerializer.Serialize(new Position(projectId, admin, request.Category, request.Timing,
                request.From, request.To, request.ActorId, snapshot, last.OccurredAt, last.Code, last.ResourceId)));
        }
        // Capture the boundary before writing our own audit. Paging does not chase its own access records.
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = admin, ProjectId = projectId, Action = "AdminTimelineViewed" });
        db.AdminAccessLogs.AddRange(page.Select(item => new AdminAccessLog { AdminId = admin, ProjectId = projectId,
            ResourceId = item.ResourceId, Action = "AdminTimelineViewedRecord" }));
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        return Ok(new AdminTimelinePage(projectId, snapshot, page, next));
    }
}
