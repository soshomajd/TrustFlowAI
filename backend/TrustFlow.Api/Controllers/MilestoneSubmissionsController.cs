using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Services.Files;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/milestones/{milestoneId:guid}")]
public sealed class MilestoneSubmissionsController(
    AppDbContext dbContext,
    IMalwareScanner malwareScanner,
    MilestoneDeliveryFileValidator fileValidator) : ControllerBase
{
    private const long MaximumRequestSize =
        MilestoneDeliveryFileValidator.MaximumTotalSize + 512 * 1024;

    [Authorize(Roles = AppRoles.Freelancer)]
    [EnableRateLimiting(RateLimitPolicies.MilestoneDelivery)]
    [HttpPost("submissions")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumRequestSize)]
    public async Task<IActionResult> Submit(
        Guid projectId,
        Guid milestoneId,
        [FromForm] SubmitMilestoneRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var freelancerId)) return Unauthorized();

        var note = request.Note.Trim();
        var previewLink = NormalizeUrl(request.PreviewLink);
        var sourceFilesLink = NormalizeUrl(request.SourceFilesLink);
        if (previewLink is null && !string.IsNullOrWhiteSpace(request.PreviewLink) ||
            sourceFilesLink is null && !string.IsNullOrWhiteSpace(request.SourceFilesLink))
            return BadRequest(new { message = "Use full http:// or https:// links without embedded credentials." });

        if (request.Files.Count > MilestoneDeliveryFileValidator.MaximumFileCount)
            return BadRequest(new { message = "You can attach up to 5 files." });
        if (request.Files.Sum(file => file.Length) > MilestoneDeliveryFileValidator.MaximumTotalSize)
            return BadRequest(new { message = "The total attachment size cannot exceed 100 MB." });
        if (request.Files.Count == 0 && previewLink is null && sourceFilesLink is null)
            return BadRequest(new { message = "Attach at least one file or add a delivery link." });

        var canSubmit = await dbContext.Milestones.AsNoTracking().AnyAsync(
            item => item.Id == milestoneId && item.ProjectId == projectId &&
                    item.FreelancerId == freelancerId &&
                    item.Project.Status == ProjectStatus.InProgress &&
                    item.Status == MileStoneStatus.InProgress,
            cancellationToken);
        if (!canSubmit)
            return Conflict(new { message = "Only your in-progress milestone can be submitted." });

        var attachments = new List<MilestoneSubmissionAttachment>();
        foreach (var file in request.Files)
        {
            if (file.Length is <= 0 or > MilestoneDeliveryFileValidator.MaximumFileSize)
                return BadRequest(new { message = "Each delivery file must be between 1 byte and 25 MB." });

            byte[] content;
            await using (var memory = new MemoryStream((int)file.Length))
            {
                await file.CopyToAsync(memory, cancellationToken);
                content = memory.ToArray();
            }

            var validationError = fileValidator.Validate(file.FileName, file.ContentType, content);
            if (validationError is not null) return BadRequest(new { message = validationError });

            var scan = await malwareScanner.ScanAsync(content, cancellationToken);
            if (!scan.IsAvailable)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "File security scanning is temporarily unavailable. Try again later." });
            if (!scan.IsClean)
                return BadRequest(new { message = "A delivery file did not pass the security scan." });

            attachments.Add(new MilestoneSubmissionAttachment
            {
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType.ToLowerInvariant(),
                FileSize = content.Length,
                Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                Content = content
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var milestone = await dbContext.Milestones
            .Include(item => item.Project)
            .SingleOrDefaultAsync(item => item.Id == milestoneId && item.ProjectId == projectId,
                cancellationToken);
        if (milestone is null || milestone.FreelancerId != freelancerId)
            return NotFound(new { message = "Milestone not found." });
        if (milestone.Project.Status != ProjectStatus.InProgress ||
            milestone.Status != MileStoneStatus.InProgress)
            return Conflict(new { message = "Only an in-progress milestone can be submitted." });

        var version = await dbContext.MilestoneSubmissions
            .Where(item => item.MilestoneId == milestoneId)
            .Select(item => (int?)item.Version)
            .MaxAsync(cancellationToken) ?? 0;
        var submission = new MilestoneSubmission
        {
            MilestoneId = milestoneId,
            FreelancerId = freelancerId,
            Version = version + 1,
            Note = note,
            PreviewLink = previewLink,
            SourceFilesLink = sourceFilesLink,
            Attachments = attachments
        };
        dbContext.MilestoneSubmissions.Add(submission);
        milestone.Status = MileStoneStatus.Submitted;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { message = "This milestone was submitted in another window. Reload and try again." });
        }

        return Ok(ToResponse(submission));
    }

    [HttpGet("submissions")]
    public async Task<IActionResult> List(
        Guid projectId, Guid milestoneId, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await CanAccessAsync(projectId, milestoneId, userId, cancellationToken))
            return NotFound(new { message = "Milestone not found." });

        var submissions = await dbContext.MilestoneSubmissions.AsNoTracking()
            .Where(item => item.MilestoneId == milestoneId)
            .Include(item => item.Attachments)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);
        return Ok(submissions.Select(ToResponse));
    }

    [HttpGet("submissions/{submissionId:guid}/files/{attachmentId:guid}")]
    public async Task<IActionResult> Download(
        Guid projectId, Guid milestoneId, Guid submissionId, Guid attachmentId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!await CanAccessAsync(projectId, milestoneId, userId, cancellationToken))
            return NotFound(new { message = "Milestone not found." });

        var attachment = await dbContext.MilestoneSubmissionAttachments.AsNoTracking()
            .Where(item => item.Id == attachmentId && item.SubmissionId == submissionId &&
                           item.Submission.MilestoneId == milestoneId)
            .Select(item => new { item.Content, item.ContentType, item.OriginalFileName })
            .SingleOrDefaultAsync(cancellationToken);
        if (attachment is null) return NotFound(new { message = "Delivery file not found." });

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(attachment.Content, attachment.ContentType, attachment.OriginalFileName,
            enableRangeProcessing: false);
    }

    [Authorize(Roles = AppRoles.Client)]
    [HttpPatch("request-changes")]
    public async Task<IActionResult> RequestChanges(
        Guid projectId, Guid milestoneId, RequestMilestoneChangesRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var clientId)) return Unauthorized();
        var milestone = await dbContext.Milestones
            .Include(item => item.Project)
            .SingleOrDefaultAsync(item => item.Id == milestoneId && item.ProjectId == projectId,
                cancellationToken);
        if (milestone is null || milestone.Project.ClientId != clientId)
            return NotFound(new { message = "Milestone not found." });
        if (milestone.Project.Status != ProjectStatus.InProgress ||
            milestone.Status != MileStoneStatus.Submitted)
            return Conflict(new { message = "Only a submitted milestone can be returned for changes." });

        var submission = await dbContext.MilestoneSubmissions
            .Where(item => item.MilestoneId == milestoneId)
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (submission is null)
            return Conflict(new { message = "No delivery was found for this milestone." });

        submission.ClientFeedback = request.Reason.Trim();
        submission.ReviewedAt = DateTimeOffset.UtcNow;
        milestone.Status = MileStoneStatus.Rejected;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new MilestoneStatusResponse { Id = milestoneId, Status = milestone.Status });
    }

    private async Task<bool> CanAccessAsync(
        Guid projectId, Guid milestoneId, Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Milestones.AsNoTracking().AnyAsync(
            item => item.Id == milestoneId && item.ProjectId == projectId &&
                    (item.Project.ClientId == userId || item.FreelancerId == userId),
            cancellationToken);

    private static string? NormalizeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo)) return null;
        return uri.AbsoluteUri;
    }

    private static MilestoneSubmissionResponse ToResponse(MilestoneSubmission submission) => new()
    {
        Id = submission.Id,
        MilestoneId = submission.MilestoneId,
        Version = submission.Version,
        Note = submission.Note,
        PreviewLink = submission.PreviewLink,
        SourceFilesLink = submission.SourceFilesLink,
        ClientFeedback = submission.ClientFeedback,
        SubmittedAt = submission.SubmittedAt,
        ReviewedAt = submission.ReviewedAt,
        Attachments = submission.Attachments.Select(item =>
            new MilestoneSubmissionAttachmentResponse
            {
                Id = item.Id,
                FileName = item.OriginalFileName,
                ContentType = item.ContentType,
                FileSize = item.FileSize,
                Sha256 = item.Sha256
            }).ToList()
    };

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
