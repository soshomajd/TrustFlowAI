using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Dtos.Common;
using TrustFlow.Api.Models;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/projects/{projectId:guid}/conversations")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminConversationsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] AdminPageQuery request, CancellationToken cancellationToken)
    {
        if (!await db.Projects.AnyAsync(item => item.Id == projectId, cancellationToken)) return NotFound();
        // Historical threads remain inspectable after assignments/proposals change.
        // The list deliberately contains no message bodies or participant read markers.
        var query = db.ChatThreads.AsNoTracking().Where(item => item.ProjectId == projectId);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.LastMessageAt).ThenBy(item => item.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(item => new AdminConversationThread(item.Id, item.Project.ClientId,
                item.Project.Client != null ? item.Project.Client.FullName : "Former client",
                item.FreelancerId, item.Freelancer.FullName, item.CreatedAt, item.LastMessageAt, item.Messages.Count))
            .ToListAsync(cancellationToken);
        await AuditAsync(projectId, "AdminConversationsViewed", null, items.Select(item => item.Id), cancellationToken);
        var pages = (int)Math.Ceiling(count / (double)request.PageSize);
        return Ok(new PagedResponse<AdminConversationThread> { Items = items, Page = request.Page, PageSize = request.PageSize,
            TotalItems = count, TotalPages = pages, HasPreviousPage = request.Page > 1, HasNextPage = request.Page < pages });
    }

    [HttpGet("{threadId:guid}/messages")]
    public async Task<IActionResult> Messages(Guid projectId, Guid threadId, [FromQuery] AdminConversationMessageQuery request,
        CancellationToken cancellationToken)
    {
        var thread = await db.ChatThreads.AsNoTracking().Where(item => item.Id == threadId && item.ProjectId == projectId)
            .Select(item => new { item.FreelancerId, item.Project.ClientId }).SingleOrDefaultAsync(cancellationToken);
        if (thread is null) return NotFound();
        var query = db.ChatMessages.AsNoTracking().Where(item => item.ThreadId == threadId);
        var through = await query.Where(item => !request.ThroughMessageId.HasValue || item.Id == request.ThroughMessageId.Value)
            .OrderByDescending(item => item.SentAt).ThenByDescending(item => item.Id)
            .Select(item => new { item.Id, item.SentAt }).FirstOrDefaultAsync(cancellationToken);
        if (request.ThroughMessageId.HasValue && through is null) return NotFound();
        if (request.BeforeMessageId is Guid beforeId)
        {
            var before = await query.Where(item => item.Id == beforeId).Select(item => new { item.Id, item.SentAt }).SingleOrDefaultAsync(cancellationToken);
            if (before is null) return NotFound();
            if (through is null || before.SentAt > through.SentAt || (before.SentAt == through.SentAt && before.Id.CompareTo(through.Id) > 0))
                return BadRequest(new { message = "The older-message cursor is outside this history snapshot." });
            query = query.Where(item => item.SentAt < before.SentAt || (item.SentAt == before.SentAt && item.Id.CompareTo(before.Id) < 0));
        }
        var messages = new List<AdminConversationMessage>();
        var hasOlder = false;
        if (through is not null)
        {
            query = query.Where(item => item.SentAt < through.SentAt || (item.SentAt == through.SentAt && item.Id.CompareTo(through.Id) <= 0));
            var page = await query.OrderByDescending(item => item.SentAt).ThenByDescending(item => item.Id).Take(request.PageSize + 1)
                .Select(item => new AdminConversationMessage(item.Id, item.SenderId, item.Sender.FullName,
                    item.SenderId == thread.FreelancerId ? "Freelancer" : item.SenderId == thread.ClientId ? "Client" : "Other participant",
                    item.Body, item.SentAt)).ToListAsync(cancellationToken);
            hasOlder = page.Count > request.PageSize;
            messages = page.Take(request.PageSize).Reverse().ToList();
        }
        // Do not use the participant Conversation endpoint: it marks messages read.
        // Only independent admin audit entries are written here.
        await AuditAsync(projectId, "AdminConversationMessagesViewed", threadId, messages.Select(item => item.Id), cancellationToken);
        return Ok(new AdminConversationMessages(threadId, through?.Id, hasOlder, hasOlder ? messages[0].Id : null, messages));
    }

    private async Task AuditAsync(Guid projectId, string action, Guid? resourceId, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var actor = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = resourceId, Action = action });
        db.AdminAccessLogs.AddRange(ids.Select(id => new AdminAccessLog { AdminId = actor, ProjectId = projectId, ResourceId = id, Action = action + "Record" }));
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
    }

}
