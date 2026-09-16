using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Clients;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Services.Files;
using TrustFlow.Api.Services.Identity;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController(
    AppDbContext dbContext,
    IMalwareScanner malwareScanner,
    PdfResumeValidator pdfValidator,
    IIdentityDataProtector identityProtector) : ControllerBase
{
    [Authorize(Roles = AppRoles.Client)]
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await ReadProfileAsync(userId, includePrivateVerification: true, cancellationToken);
    }

    [HttpGet("{clientId:guid}/profile")]
    public Task<IActionResult> GetProfile(Guid clientId, CancellationToken cancellationToken) =>
        ReadProfileAsync(clientId, includePrivateVerification: false, cancellationToken);

    [Authorize(Roles = AppRoles.Client)]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var verification = await dbContext.ClientIdentityVerifications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientId == userId, cancellationToken);
        if (verification is { Status: "Pending" or "Verified" } &&
            verification.AccountType != request.AccountType)
            return Conflict(new { message = "Reset identity verification before changing the account type." });

        var profile = await dbContext.ClientProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new ClientProfile { UserId = userId };
            dbContext.ClientProfiles.Add(profile);
        }

        profile.AccountType = request.AccountType;
        profile.CompanyName = request.AccountType == "Company" ? request.CompanyName.Trim() : string.Empty;
        profile.Overview = request.Overview.Trim();
        profile.Industry = request.Industry.Trim();
        profile.Location = request.Location.Trim();
        profile.Website = request.Website.Trim();
        profile.CompanySize = request.AccountType == "Company" ? request.CompanySize : "NotSpecified";
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { message = "Your profile was saved in another window. Reload and try again." });
        }

        return await ReadProfileAsync(userId, includePrivateVerification: true, cancellationToken);
    }

    [Authorize(Roles = AppRoles.Client)]
    [EnableRateLimiting(RateLimitPolicies.IdentitySubmission)]
    [HttpPost("me/verification")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(PdfResumeValidator.MaximumFileSize + 96 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = PdfResumeValidator.MaximumFileSize + 96 * 1024)]
    public async Task<IActionResult> SubmitVerification(
        [FromForm] SubmitClientVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (request.File is null)
            return BadRequest(new { message = "A PDF identity document is required." });

        var profile = await dbContext.ClientProfiles.SingleOrDefaultAsync(
            item => item.UserId == userId, cancellationToken);
        var accountType = profile?.AccountType ?? "Individual";
        if (accountType == "Company" && string.IsNullOrWhiteSpace(profile?.CompanyName))
            return Conflict(new { message = "Save the company name before requesting verification." });
        if (accountType == "Company" && string.IsNullOrWhiteSpace(request.RegistrationNumber))
            return BadRequest(new { message = "Company registration number is required." });

        var existing = await dbContext.ClientIdentityVerifications.SingleOrDefaultAsync(
            item => item.ClientId == userId, cancellationToken);
        if (existing is { Status: "Pending" })
            return Conflict(new { message = "Your verification is already waiting for review." });
        if (existing is { Status: "Verified" })
            return Conflict(new { message = "Your identity is already verified. Reset it first if details changed." });

        var file = request.File;
        if (file.Length is <= 0 or > PdfResumeValidator.MaximumFileSize)
            return BadRequest(new { message = "The identity document must be a PDF no larger than 5 MB." });
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Only PDF identity documents are allowed." });

        byte[] content;
        await using (var memory = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(memory, cancellationToken);
            content = memory.ToArray();
        }

        var scan = await malwareScanner.ScanAsync(content, cancellationToken);
        if (!scan.IsAvailable)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Document security scanning is temporarily unavailable. Try again later." });
        if (!scan.IsClean)
            return BadRequest(new { message = "The document did not pass the security scan." });

        var validationError = pdfValidator.Validate(content);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var verification = existing ?? new ClientIdentityVerification { ClientId = userId };
        if (existing is null) dbContext.ClientIdentityVerifications.Add(verification);
        verification.AccountType = accountType;
        verification.LegalNameProtected = identityProtector.Protect(request.LegalName.Trim());
        verification.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
        verification.RegistrationNumberProtected = accountType == "Company"
            ? identityProtector.Protect(request.RegistrationNumber!.Trim()) : null;
        verification.DocumentContentProtected = identityProtector.Protect(content);
        verification.DocumentFileSize = content.Length;
        verification.DocumentSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        verification.Status = "Pending";
        verification.SubmittedAt = DateTimeOffset.UtcNow;
        verification.ReviewedAt = null;
        verification.ReviewedByUserId = null;
        verification.RejectionReason = string.Empty;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReadProfileAsync(userId, includePrivateVerification: true, cancellationToken);
    }

    [Authorize(Roles = AppRoles.Client)]
    [HttpDelete("me/verification")]
    public async Task<IActionResult> ResetVerification(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        await dbContext.ClientIdentityVerifications.Where(item => item.ClientId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> ReadProfileAsync(
        Guid userId,
        bool includePrivateVerification,
        CancellationToken cancellationToken)
    {
        if (!await IsClientAsync(userId, cancellationToken))
            return NotFound(new { message = "Client not found." });

        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Id, item.FullName, item.CreatedAt, item.WalletVerifiedAt })
            .SingleAsync(cancellationToken);
        var profile = await dbContext.ClientProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        var verification = await dbContext.ClientIdentityVerifications.AsNoTracking()
            .SingleOrDefaultAsync(item => item.ClientId == userId, cancellationToken);
        var projects = dbContext.Projects.AsNoTracking().Where(item => item.ClientId == userId);

        var projectsPosted = await projects.CountAsync(cancellationToken);
        var activeProjects = await projects.CountAsync(item =>
            item.Status == ProjectStatus.Open || item.Status == ProjectStatus.InProgress, cancellationToken);
        var completedProjects = await projects.CountAsync(item =>
            item.Status == ProjectStatus.Completed, cancellationToken);
        var totalHires = await projects.CountAsync(item => item.FreelancerId != null, cancellationToken);
        var accountType = profile?.AccountType ?? "Individual";
        var verified = verification?.Status == "Verified";

        return Ok(new ClientProfileResponse
        {
            UserId = user.Id,
            DisplayName = accountType == "Company" && !string.IsNullOrWhiteSpace(profile?.CompanyName)
                ? profile.CompanyName : user.FullName,
            AccountType = accountType,
            CompanyName = profile?.CompanyName ?? string.Empty,
            Overview = profile?.Overview ?? string.Empty,
            Industry = profile?.Industry ?? string.Empty,
            Location = profile?.Location ?? string.Empty,
            Website = profile?.Website ?? string.Empty,
            CompanySize = profile?.CompanySize ?? "NotSpecified",
            MemberSince = user.CreatedAt,
            WalletVerified = user.WalletVerifiedAt.HasValue,
            IdentityVerified = verified,
            IdentityVerifiedAt = verified ? verification!.ReviewedAt : null,
            VerificationStatus = includePrivateVerification
                ? verification?.Status ?? "NotSubmitted"
                : verified ? "Verified" : "NotSubmitted",
            VerificationSubmittedAt = includePrivateVerification ? verification?.SubmittedAt : null,
            VerificationRejectionReason = includePrivateVerification && verification?.Status == "Rejected"
                ? verification.RejectionReason : string.Empty,
            ProjectsPosted = projectsPosted,
            ActiveProjects = activeProjects,
            CompletedProjects = completedProjects,
            TotalHires = totalHires,
            UpdatedAt = profile?.UpdatedAt
        });
    }

    private Task<bool> IsClientAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.UserRoles
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == AppRoles.Client
         select userRole).AnyAsync(cancellationToken);

    private bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
