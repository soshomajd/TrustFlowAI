using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Clients;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
public sealed class ClientsController(
    AppDbContext dbContext) : ControllerBase
{
    [Authorize(Roles = AppRoles.Client)]
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await ReadProfileAsync(userId, cancellationToken);
    }

    [HttpGet("{clientId:guid}/profile")]
    public Task<IActionResult> GetProfile(Guid clientId, CancellationToken cancellationToken) =>
        ReadProfileAsync(clientId, cancellationToken);

    [Authorize(Roles = AppRoles.Client)]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateClientProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

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

        return await ReadProfileAsync(userId, cancellationToken);
    }

    private async Task<IActionResult> ReadProfileAsync(
        Guid userId,
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
        var projects = dbContext.Projects.AsNoTracking().Where(item => item.ClientId == userId);

        var projectsPosted = await projects.CountAsync(cancellationToken);
        var activeProjects = await projects.CountAsync(item =>
            item.Status == ProjectStatus.Open || item.Status == ProjectStatus.InProgress, cancellationToken);
        var completedProjects = await projects.CountAsync(item =>
            item.Status == ProjectStatus.Completed, cancellationToken);
        var totalHires = await projects.CountAsync(item => item.FreelancerId != null, cancellationToken);
        var accountType = profile?.AccountType ?? "Individual";

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
