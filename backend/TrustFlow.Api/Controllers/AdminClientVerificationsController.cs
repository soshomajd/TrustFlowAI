using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Clients;
using TrustFlow.Api.Services.Identity;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/client-verifications")]
public sealed class AdminClientVerificationsController(
    AppDbContext dbContext,
    IIdentityDataProtector identityProtector) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        var pending = await (
            from verification in dbContext.ClientIdentityVerifications.AsNoTracking()
            join user in dbContext.Users.AsNoTracking() on verification.ClientId equals user.Id
            join profileValue in dbContext.ClientProfiles.AsNoTracking()
                on verification.ClientId equals profileValue.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            where verification.Status == "Pending"
            orderby verification.SubmittedAt, verification.ClientId
            select new
            {
                verification.ClientId,
                user.FullName,
                user.Email,
                verification.AccountType,
                verification.LegalNameProtected,
                verification.CountryCode,
                verification.RegistrationNumberProtected,
                verification.DocumentFileSize,
                verification.SubmittedAt,
                CompanyName = profile == null ? string.Empty : profile.CompanyName
            }).ToListAsync(cancellationToken);

        return Ok(pending.Select(item => new ClientVerificationAdminResponse
        {
            ClientId = item.ClientId,
            DisplayName = item.AccountType == "Company" && !string.IsNullOrWhiteSpace(item.CompanyName)
                ? item.CompanyName : item.FullName,
            Email = item.Email ?? string.Empty,
            AccountType = item.AccountType,
            LegalName = item.LegalNameProtected is null ? string.Empty : identityProtector.UnprotectString(item.LegalNameProtected),
            CountryCode = item.CountryCode,
            RegistrationNumber = item.RegistrationNumberProtected is null
                ? string.Empty : identityProtector.UnprotectString(item.RegistrationNumberProtected),
            DocumentFileSize = item.DocumentFileSize ?? 0,
            SubmittedAt = item.SubmittedAt
        }));
    }

    [HttpGet("{clientId:guid}/document")]
    public async Task<IActionResult> DownloadDocument(Guid clientId, CancellationToken cancellationToken)
    {
        var protectedContent = await dbContext.ClientIdentityVerifications.AsNoTracking()
            .Where(item => item.ClientId == clientId && item.Status == "Pending")
            .Select(item => item.DocumentContentProtected)
            .SingleOrDefaultAsync(cancellationToken);
        if (protectedContent is null) return NotFound(new { message = "Pending verification document not found." });

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";
        return File(identityProtector.Unprotect(protectedContent), "application/pdf",
            "identity-document.pdf", enableRangeProcessing: false);
    }

    [HttpPost("{clientId:guid}/decision")]
    public async Task<IActionResult> Review(
        Guid clientId,
        ReviewClientVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId))
            return Unauthorized();
        var reason = request.Reason.Trim();
        if (!request.Approve && string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "A short reason is required when verification is rejected." });
        if (reason.Length > 300)
            return BadRequest(new { message = "Review reason cannot exceed 300 characters." });

        var verification = await dbContext.ClientIdentityVerifications.SingleOrDefaultAsync(
            item => item.ClientId == clientId && item.Status == "Pending", cancellationToken);
        if (verification is null)
            return NotFound(new { message = "Pending verification not found." });

        verification.Status = request.Approve ? "Verified" : "Rejected";
        verification.ReviewedAt = DateTimeOffset.UtcNow;
        verification.ReviewedByUserId = reviewerId;
        verification.RejectionReason = request.Approve ? string.Empty : reason;
        verification.DocumentContentProtected = null;
        verification.DocumentFileSize = null;
        verification.LegalNameProtected = null;
        verification.RegistrationNumberProtected = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
