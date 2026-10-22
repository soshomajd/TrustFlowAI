using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Dtos.Common;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/projects/{projectId:guid}")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminProjectDossierController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await db.Projects.AsNoTracking().Where(item => item.Id == projectId)
            .Select(item => new AdminDossierResponse(item.Id, item.Title, item.Description, item.Status,
                item.ClientId, item.Client != null ? item.Client.FullName : "Former client", item.Budget,
                item.Deadline, item.CreatedAt, 0, item.Milestones.Count, item.Proposals.Count, item.Escrows.Count,
                db.MilestoneSubmissions.Count(delivery => delivery.Milestone.ProjectId == projectId)))
            .SingleOrDefaultAsync(cancellationToken);
        if (project is null) return NotFound();
        project = project with { ParticipantCount = await Participants(projectId).CountAsync(cancellationToken) };
        await AuditAsync(projectId, "AdminProjectViewed", [], cancellationToken);
        return Ok(project);
    }

    [HttpGet("{section}")]
    public async Task<IActionResult> Section(Guid projectId, string section, [FromQuery] AdminPageQuery request,
        CancellationToken cancellationToken)
    {
        if (section is not ("participants" or "milestones" or "proposals" or "escrows" or "deliveries")) return NotFound();
        if (!await db.Projects.AnyAsync(item => item.Id == projectId, cancellationToken)) return NotFound();
        switch (section)
        {
            case "participants":
                return await PageAsync(projectId, "Participants", Participants(projectId).OrderBy(item => item.FullName).ThenBy(item => item.Id)
                    .Select(user => new AdminDossierParticipant(user.Id, user.FullName,
                        db.Projects.Any(item => item.Id == projectId && item.ClientId == user.Id),
                        db.Milestones.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id),
                        db.Escrows.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id),
                        db.Proposals.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id),
                        user.WalletAddress, user.WalletVerifiedAt != null,
                        db.MilestoneSubmissions.Any(item => item.Milestone.ProjectId == projectId && item.FreelancerId == user.Id))), request, item => item.Id, cancellationToken);
            case "milestones":
                return await PageAsync(projectId, "Milestones", db.Milestones.AsNoTracking().Where(item => item.ProjectId == projectId)
                    .OrderBy(item => item.SequenceNumber).ThenBy(item => item.Id)
                    .Select(item => new AdminDossierMilestone(item.Id, item.Title, item.Description, item.SequenceNumber,
                        item.Status, item.Amount, item.Deadline, item.FreelancerId, item.Freelancer != null ? item.Freelancer.FullName : null)),
                    request, item => item.Id, cancellationToken);
            case "proposals":
                return await PageAsync(projectId, "Proposals", db.Proposals.AsNoTracking().Where(item => item.ProjectId == projectId)
                    .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
                    .Select(item => new AdminDossierProposal(item.Id, item.FreelancerId, item.Freelancer.FullName,
                        item.Status, item.CoverLetter, item.BidAmount, item.EstimatedDays, item.ProposalMilestones.Count, item.CreatedAt)),
                    request, item => item.Id, cancellationToken);
            case "deliveries":
                return await PageAsync(projectId, "Deliveries", db.MilestoneSubmissions.AsNoTracking()
                    .Where(item => item.Milestone.ProjectId == projectId)
                    .OrderBy(item => item.Milestone.SequenceNumber).ThenByDescending(item => item.Version).ThenBy(item => item.Id)
                    .Select(item => new AdminDossierDelivery(item.Id, item.MilestoneId, item.Milestone.Title,
                        item.Milestone.SequenceNumber, item.Milestone.Status, item.FreelancerId,
                        db.Users.Where(user => user.Id == item.FreelancerId).Select(user => user.FullName).FirstOrDefault() ?? "Former freelancer",
                        item.Version, !db.MilestoneSubmissions.Any(later => later.MilestoneId == item.MilestoneId && later.Version > item.Version),
                        item.Note, item.PreviewLink, item.SourceFilesLink, item.ClientFeedback, item.SubmittedAt, item.ReviewedAt,
                        item.Attachments.OrderBy(file => file.Id).Select(file => new AdminDeliveryAttachment(file.Id,
                            file.OriginalFileName, file.ContentType, file.FileSize, file.Sha256)).ToList())),
                    request, item => item.Id, cancellationToken);
            default:
                return await PageAsync(projectId, "Escrows", db.Escrows.AsNoTracking().Where(item => item.ProjectId == projectId)
                    .OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
                    .Select(item => new AdminDossierEscrow(item.Id, item.FreelancerId,
                        db.Users.Where(user => user.Id == item.FreelancerId).Select(user => user.FullName).FirstOrDefault() ?? "Former freelancer",
                        item.Status, item.ChainId, item.TokenAddress, item.ContractAddress, item.ClientWalletAddress,
                        item.FreelancerWalletAddress, item.ArbiterWalletAddress, item.FeeRecipientAddress,
                        item.PlatformFeeBasisPoints, item.TokenDecimals, item.TotalAmount, item.ReleasedAmount, item.CreatedAt, item.UpdatedAt)),
                    request, item => item.Id, cancellationToken);
        }
    }

    private IQueryable<ApplicationUser> Participants(Guid projectId) => db.Users.AsNoTracking().Where(user =>
        db.Projects.Any(item => item.Id == projectId && item.ClientId == user.Id)
        || db.Milestones.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id)
        || db.Escrows.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id)
        || db.Proposals.Any(item => item.ProjectId == projectId && item.FreelancerId == user.Id)
        || db.MilestoneSubmissions.Any(item => item.Milestone.ProjectId == projectId && item.FreelancerId == user.Id));

    [HttpGet("deliveries/{submissionId:guid}/files/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid projectId, Guid submissionId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var file = await db.MilestoneSubmissionAttachments.AsNoTracking()
            .Where(item => item.Id == attachmentId && item.SubmissionId == submissionId
                && item.Submission.Milestone.ProjectId == projectId)
            .Select(item => new { item.Content, item.OriginalFileName }).SingleOrDefaultAsync(cancellationToken);
        if (file is null) return NotFound();
        await AuditAsync(projectId, "AdminDeliveryFileDownloaded", [attachmentId], cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(file.Content, "application/octet-stream", file.OriginalFileName, enableRangeProcessing: false);
    }

    private async Task<IActionResult> PageAsync<T>(Guid projectId, string section, IQueryable<T> query,
        AdminPageQuery request, Func<T, Guid> id, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        await AuditAsync(projectId, "AdminProject" + section + "Viewed", items.Select(id), cancellationToken);
        var pages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Ok(new PagedResponse<T> { Items = items, Page = request.Page, PageSize = request.PageSize,
            TotalItems = total, TotalPages = pages, HasPreviousPage = request.Page > 1, HasNextPage = request.Page < pages });
    }

    private async Task AuditAsync(Guid projectId, string action, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = adminId, ProjectId = projectId, Action = action });
        db.AdminAccessLogs.AddRange(ids.Select(id => new AdminAccessLog { AdminId = adminId, ProjectId = projectId,
            ResourceId = id, Action = action + "Record" }));
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
    }
}
