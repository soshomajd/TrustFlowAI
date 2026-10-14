using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Chat;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Services.Chat;

namespace TrustFlow.Api.Controllers;

// Messages between a project's client and one freelancer. A conversation is
// tied to a project and to a freelancer who applied to it or holds some of its
// milestones, so nobody can message a stranger, and what is said to one
// freelancer is never shown to another. Text only: work goes through the
// delivery form, and phone numbers, emails, links and messenger handles are
// refused because they would take the conversation (and the payment) out of
// reach of the escrow.
[ApiController]
[Authorize]
public sealed class ChatController(
    AppDbContext dbContext)
    : ControllerBase
{
    [HttpGet("api/chat")]
    public async Task<IActionResult> Inbox(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var rows = await dbContext.ChatThreads
            .AsNoTracking()
            .Where(thread =>
                thread.FreelancerId == userId ||
                thread.Project.ClientId == userId)
            .OrderByDescending(thread => thread.LastMessageAt)
            .Take(100)
            .Select(thread => new
            {
                thread.ProjectId,
                ProjectTitle = thread.Project.Title,
                thread.FreelancerId,
                FreelancerName = thread.Freelancer.FullName,
                ClientId = thread.Project.ClientId,
                ClientName = thread.Project.Client!.FullName,
                thread.LastMessageAt,
                thread.ClientLastReadAt,
                thread.FreelancerLastReadAt,
                Last = thread.Messages
                    .OrderByDescending(message => message.SentAt)
                    .Select(message => new
                    {
                        message.Body,
                        message.SenderId
                    })
                    .FirstOrDefault(),
                FromClient = thread.Messages.Count(message =>
                    message.SenderId == thread.Project.ClientId &&
                    message.SentAt > thread.FreelancerLastReadAt),
                FromFreelancer = thread.Messages.Count(message =>
                    message.SenderId == thread.FreelancerId &&
                    message.SentAt > thread.ClientLastReadAt)
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Where(row => row.Last is not null)
            .Select(row =>
            {
                var viewerIsFreelancer = row.FreelancerId == userId;

                return new ChatInboxItemResponse
                {
                    ProjectId = row.ProjectId,
                    ProjectTitle = row.ProjectTitle,
                    FreelancerId = row.FreelancerId,
                    Counterpart = viewerIsFreelancer
                        ? new ChatCounterpartResponse
                        {
                            Id = row.ClientId ?? Guid.Empty,
                            FullName = row.ClientName,
                            Role = AppRoles.Client
                        }
                        : new ChatCounterpartResponse
                        {
                            Id = row.FreelancerId,
                            FullName = row.FreelancerName,
                            Role = AppRoles.Freelancer
                        },
                    LastMessage = Preview(row.Last!.Body),
                    LastMessageMine = row.Last.SenderId == userId,
                    LastMessageAt = row.LastMessageAt,
                    UnreadCount = viewerIsFreelancer
                        ? row.FromClient
                        : row.FromFreelancer
                };
            })
            .ToList();

        return Ok(new ChatInboxResponse
        {
            Items = items,
            UnreadCount = items.Sum(item => item.UnreadCount)
        });
    }

    [HttpGet("api/projects/{projectId:guid}/chat/{freelancerId:guid}")]
    public async Task<IActionResult> Conversation(
        Guid projectId,
        Guid freelancerId,
        [FromQuery] DateTimeOffset? after,
        [FromQuery] Guid? afterMessageId,
        [FromQuery] Guid? beforeMessageId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var access = await ResolveAsync(
            projectId, freelancerId, userId, cancellationToken);

        if (access is null)
        {
            return NotFound(new { message = "This conversation does not exist." });
        }

        if ((beforeMessageId is not null && (after is not null || afterMessageId is not null)) ||
            (after is not null && afterMessageId is not null))
            return BadRequest(new { message = "Choose one message cursor." });

        var messages = new List<ChatMessageResponse>();
        var hasOlderMessages = false;

        if (access.ThreadId is null && (beforeMessageId is not null || afterMessageId is not null))
            return NotFound(new { message = "This message is not in this conversation." });

        if (access.ThreadId is Guid threadId)
        {
            var query = dbContext.ChatMessages
                .AsNoTracking()
                .Where(message => message.ThreadId == threadId);

            var cursorId = beforeMessageId ?? afterMessageId;
            if (cursorId is Guid id)
            {
                var cursor = await query.FirstOrDefaultAsync(message => message.Id == id, cancellationToken);
                if (cursor is null)
                    return NotFound(new { message = "This message is not in this conversation." });
                query = beforeMessageId is not null
                    ? query.Where(message => message.SentAt < cursor.SentAt ||
                        (message.SentAt == cursor.SentAt && message.Id.CompareTo(cursor.Id) < 0))
                    : query.Where(message => message.SentAt > cursor.SentAt ||
                        (message.SentAt == cursor.SentAt && message.Id.CompareTo(cursor.Id) > 0));
            }
            else if (after is { } since)
                query = query.Where(message => message.SentAt > since);

            var polling = after is not null || afterMessageId is not null;
            if (polling)
            {
                messages = (await query.OrderBy(message => message.SentAt).ThenBy(message => message.Id)
                        .Take(ChatLimits.OpeningPage).ToListAsync(cancellationToken))
                    .Select(message => ToResponse(message, userId)).ToList();
            }
            else
            {
                var page = await query.OrderByDescending(message => message.SentAt)
                    .ThenByDescending(message => message.Id).Take(ChatLimits.OpeningPage + 1)
                    .ToListAsync(cancellationToken);
                hasOlderMessages = page.Count > ChatLimits.OpeningPage;
                messages = page.Take(ChatLimits.OpeningPage).Reverse()
                    .Select(message => ToResponse(message, userId)).ToList();
            }

            // Reading older history must not mark newly arriving messages read.
            if (beforeMessageId is null)
                await MarkReadAsync(threadId, access.ViewerIsClient, cancellationToken);
        }

        return Ok(new ChatConversationResponse
        {
            ProjectId = projectId,
            ProjectTitle = access.ProjectTitle,
            FreelancerId = freelancerId,
            Counterpart = access.Counterpart,
            CanSend = access.CannotSendReason is null,
            CannotSendReason = access.CannotSendReason,
            Messages = messages,
            HasOlderMessages = hasOlderMessages
        });
    }

    [HttpPost("api/projects/{projectId:guid}/chat/{freelancerId:guid}/messages")]
    [EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<IActionResult> Send(
        Guid projectId,
        Guid freelancerId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var access = await ResolveAsync(
            projectId, freelancerId, userId, cancellationToken);

        if (access is null)
        {
            return NotFound(new { message = "This conversation does not exist." });
        }

        if (access.CannotSendReason is not null)
        {
            return Conflict(new { message = access.CannotSendReason });
        }

        var body = request.Body.Trim();

        if (body.Length == 0)
        {
            return BadRequest(new { message = "Write a message first." });
        }

        var contact = ContactInfoDetector.Find(body);

        if (contact.Count > 0)
        {
            return UnprocessableEntity(new
            {
                code = "contact_info",
                found = contact,
                message =
                    "This message was not sent because it looks like it " +
                    $"contains a {string.Join(" and a ", contact)}. " +
                    "Keep the conversation and the payment on TrustFlow, " +
                    "where the escrow protects both of you. Put work " +
                    "samples on your profile and send files as a delivery."
            });
        }

        var now = DateTimeOffset.UtcNow;
        var threadId = access.ThreadId;

        if (threadId is null)
        {
            var created = new ChatThread
            {
                ProjectId = projectId,
                FreelancerId = freelancerId,
                CreatedAt = now,
                LastMessageAt = now,

                // Nobody has read anything yet. The sender's marker is moved
                // to now once their message is stored, below, so the first
                // message counts as unread for the other side.
                ClientLastReadAt = DateTimeOffset.UnixEpoch,
                FreelancerLastReadAt = DateTimeOffset.UnixEpoch
            };

            dbContext.ChatThreads.Add(created);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                threadId = created.Id;
            }
            catch (DbUpdateException)
            {
                // The other side started the same conversation a moment ago.
                dbContext.Entry(created).State = EntityState.Detached;

                threadId = await dbContext.ChatThreads
                    .AsNoTracking()
                    .Where(thread =>
                        thread.ProjectId == projectId &&
                        thread.FreelancerId == freelancerId)
                    .Select(thread => (Guid?)thread.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (threadId is null)
                {
                    throw;
                }
            }
        }

        var message = new ChatMessage
        {
            ThreadId = threadId.Value,
            SenderId = userId,
            Body = body,
            SentAt = now
        };

        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Sending shows the sender the conversation up to this message, and
        // moves the thread to the top of both inboxes.
        var thisThread = dbContext.ChatThreads
            .Where(thread => thread.Id == threadId.Value);

        if (access.ViewerIsClient)
        {
            await thisThread.ExecuteUpdateAsync(
                update => update
                    .SetProperty(thread => thread.LastMessageAt, now)
                    .SetProperty(thread => thread.ClientLastReadAt, now),
                cancellationToken);
        }
        else
        {
            await thisThread.ExecuteUpdateAsync(
                update => update
                    .SetProperty(thread => thread.LastMessageAt, now)
                    .SetProperty(thread => thread.FreelancerLastReadAt, now),
                cancellationToken);
        }

        return Ok(ToResponse(message, userId));
    }

    private sealed record ChatAccess(
        string ProjectTitle,
        bool ViewerIsClient,
        ChatCounterpartResponse Counterpart,
        Guid? ThreadId,
        string? CannotSendReason);

    // Who may open this conversation, and whether they may add to it. Only
    // the project's client and the named freelancer are ever let in; a
    // freelancer who neither applied nor holds a milestone has no
    // conversation to open.
    private async Task<ChatAccess?> ResolveAsync(
        Guid projectId,
        Guid freelancerId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects
            .AsNoTracking()
            .Where(item => item.Id == projectId)
            .Select(item => new
            {
                item.Title,
                item.ClientId,
                item.Status,
                ClientName = item.Client!.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null ||
            project.ClientId is null ||
            (userId != project.ClientId && userId != freelancerId))
        {
            return null;
        }

        var freelancerName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == freelancerId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken);

        if (freelancerName is null)
        {
            return null;
        }

        var threadId = await dbContext.ChatThreads
            .AsNoTracking()
            .Where(thread =>
                thread.ProjectId == projectId &&
                thread.FreelancerId == freelancerId)
            .Select(thread => (Guid?)thread.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var taking = await dbContext.Proposals
            .AsNoTracking()
            .AnyAsync(
                proposal =>
                    proposal.ProjectId == projectId &&
                    proposal.FreelancerId == freelancerId &&
                    (proposal.Status == ProposalStatus.Pending ||
                     proposal.Status == ProposalStatus.Accepted),
                cancellationToken)
            || await dbContext.Milestones
                .AsNoTracking()
                .AnyAsync(
                    milestone =>
                        milestone.ProjectId == projectId &&
                        milestone.FreelancerId == freelancerId,
                    cancellationToken);

        if (!taking && threadId is null)
        {
            return null;
        }

        string? reason = null;

        if (project.Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
        {
            reason = "This project has finished, so the conversation is closed.";
        }
        else if (!taking)
        {
            reason =
                "This freelancer is no longer part of the project, so the " +
                "conversation is closed.";
        }

        var viewerIsClient = userId == project.ClientId;

        return new ChatAccess(
            project.Title,
            viewerIsClient,
            viewerIsClient
                ? new ChatCounterpartResponse
                {
                    Id = freelancerId,
                    FullName = freelancerName,
                    Role = AppRoles.Freelancer
                }
                : new ChatCounterpartResponse
                {
                    Id = project.ClientId.Value,
                    FullName = project.ClientName,
                    Role = AppRoles.Client
                },
            threadId,
            reason);
    }

    private Task<int> MarkReadAsync(
        Guid threadId,
        bool viewerIsClient,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Only moves forward and only when something is unread, so a poll
        // every few seconds does not write on every call.
        return viewerIsClient
            ? dbContext.ChatThreads
                .Where(thread =>
                    thread.Id == threadId &&
                    thread.ClientLastReadAt < thread.LastMessageAt)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(
                        thread => thread.ClientLastReadAt, now),
                    cancellationToken)
            : dbContext.ChatThreads
                .Where(thread =>
                    thread.Id == threadId &&
                    thread.FreelancerLastReadAt < thread.LastMessageAt)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(
                        thread => thread.FreelancerLastReadAt, now),
                    cancellationToken);
    }

    private static ChatMessageResponse ToResponse(
        ChatMessage message,
        Guid viewerId) =>
        new()
        {
            Id = message.Id,
            Mine = message.SenderId == viewerId,
            Body = message.Body,
            SentAt = message.SentAt
        };

    private static string Preview(string body)
    {
        var single = body.ReplaceLineEndings(" ");

        return single.Length <= 120 ? single : single[..117] + "...";
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
