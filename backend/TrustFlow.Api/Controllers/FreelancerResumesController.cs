using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Services.Files;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/freelancers")]
public sealed class FreelancerResumesController(
    AppDbContext dbContext,
    IMalwareScanner malwareScanner,
    PdfResumeValidator pdfValidator) : ControllerBase
{
    [Authorize(Roles = AppRoles.Freelancer)]
    [EnableRateLimiting(RateLimitPolicies.ResumeUpload)]
    [HttpPost("me/resume")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PdfResumeValidator.MaximumFileSize + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PdfResumeValidator.MaximumFileSize + 64 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (file.Length is <= 0 or > PdfResumeValidator.MaximumFileSize)
            return BadRequest(new { message = "The resume must be a PDF no larger than 5 MB." });

        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF files are allowed." });

        byte[] content;
        await using (var memory = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(memory, cancellationToken);
            content = memory.ToArray();
        }

        var scan = await malwareScanner.ScanAsync(content, cancellationToken);
        if (!scan.IsAvailable)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Resume security scanning is temporarily unavailable. Try again later." });
        if (!scan.IsClean)
            return BadRequest(new { message = "The file did not pass the security scan." });

        var validationError = pdfValidator.Validate(content);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var now = DateTimeOffset.UtcNow;
        var resume = await dbContext.FreelancerResumes.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (resume is null)
        {
            resume = new FreelancerResume { UserId = userId };
            dbContext.FreelancerResumes.Add(resume);
        }

        resume.Content = content;
        resume.FileSize = content.Length;
        resume.Sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        resume.UploadedAt = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { message = "Your resume was updated in another window. Reload and try again." });
        }

        return Ok(new { hasResume = true, resumeFileSize = resume.FileSize, resumeUploadedAt = resume.UploadedAt });
    }

    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpDelete("me/resume")]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        await dbContext.FreelancerResumes.Where(item => item.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{freelancerId:guid}/resume")]
    public async Task<IActionResult> Download(Guid freelancerId, CancellationToken cancellationToken)
    {
        var content = await dbContext.FreelancerResumes.AsNoTracking()
            .Where(item => item.UserId == freelancerId)
            .Select(item => item.Content)
            .SingleOrDefaultAsync(cancellationToken);
        if (content is null)
            return NotFound(new { message = "Resume not found." });

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(content, "application/pdf", "resume.pdf", enableRangeProcessing: false);
    }

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
