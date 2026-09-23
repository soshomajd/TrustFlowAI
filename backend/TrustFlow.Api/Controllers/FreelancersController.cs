using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Freelancers;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/freelancers")]
public sealed class FreelancersController(AppDbContext dbContext) : ControllerBase
{
    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpGet("me/profile")]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        return await ReadProfileAsync(userId, cancellationToken);
    }

    [HttpGet("{freelancerId:guid}/profile")]
    public Task<IActionResult> GetProfile(Guid freelancerId, CancellationToken cancellationToken) =>
        ReadProfileAsync(freelancerId, cancellationToken);

    [Authorize(Roles = AppRoles.Freelancer)]
    [HttpPut("me/profile")]
    public async Task<IActionResult> UpdateMyProfile(
        UpdateFreelancerProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        if (!await IsFreelancerAsync(userId, cancellationToken))
            return NotFound(new { message = "Freelancer not found." });

        var profile = await dbContext.FreelancerProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        if (profile is null)
        {
            profile = new FreelancerProfile { UserId = userId };
            dbContext.FreelancerProfiles.Add(profile);
        }

        profile.Headline = request.Headline.Trim();
        profile.About = request.About.Trim();
        profile.Location = request.Location.Trim();
        profile.YearsOfExperience = request.YearsOfExperience;
        profile.Availability = request.Availability;
        profile.Skills = NormalizeEntries(request.Skills);
        profile.Languages = NormalizeEntries(request.Languages);
        profile.PortfolioItemsJson = JsonSerializer.Serialize(request.PortfolioItems.Select(item =>
            new PortfolioItemRequest
            {
                Title = item.Title.Trim(),
                Description = item.Description.Trim(),
                Url = item.Url.Trim()
            }));
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

    private Task<bool> IsFreelancerAsync(Guid userId, CancellationToken cancellationToken) =>
        (from userRole in dbContext.UserRoles
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == AppRoles.Freelancer
         select userRole).AnyAsync(cancellationToken);

    private async Task<IActionResult> ReadProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await IsFreelancerAsync(userId, cancellationToken))
            return NotFound(new { message = "Freelancer not found." });

        var user = await dbContext.Users.AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.Id, item.FullName, item.CreatedAt, item.WalletVerifiedAt })
            .SingleAsync(cancellationToken);

        var profile = await dbContext.FreelancerProfiles.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);

        var resume = await dbContext.FreelancerResumes.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.FileSize, item.UploadedAt })
            .SingleOrDefaultAsync(cancellationToken);

        var completedQuery = dbContext.Projects.AsNoTracking()
            .Where(item => item.Milestones.Any(milestone => milestone.FreelancerId == userId) && item.Status == ProjectStatus.Completed);

        var completedCount = await completedQuery.CountAsync(cancellationToken);
        var completedProjects = await completedQuery
            // Order by when this freelancer's own escrow finished.
            .OrderByDescending(item => item.Escrows
                .Where(escrow => escrow.FreelancerId == userId)
                .Select(escrow => escrow.CompletedAt)
                .FirstOrDefault())
            .ThenByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
            .Take(10)
            .Select(item => new CompletedFreelancerProjectResponse
            {
                Title = item.Title,
                MilestoneCount = item.Milestones.Count,
                CompletedAt = item.Escrows
                    .Where(escrow => escrow.FreelancerId == userId)
                    .Select(escrow => escrow.CompletedAt)
                    .FirstOrDefault()
            }).ToArrayAsync(cancellationToken);

        return Ok(new FreelancerProfileResponse
        {
            UserId = user.Id,
            FullName = user.FullName,
            MemberSince = user.CreatedAt,
            WalletVerified = user.WalletVerifiedAt.HasValue,
            Headline = profile?.Headline ?? string.Empty,
            About = profile?.About ?? string.Empty,
            Location = profile?.Location ?? string.Empty,
            YearsOfExperience = profile?.YearsOfExperience,
            Availability = profile?.Availability ?? "NotSpecified",
            Skills = profile?.Skills ?? [],
            Languages = profile?.Languages ?? [],
            PortfolioItems = profile is null ? [] :
                JsonSerializer.Deserialize<PortfolioItemRequest[]>(profile.PortfolioItemsJson) ?? [],
            CompletedProjectCount = completedCount,
            CompletedProjects = completedProjects,
            UpdatedAt = profile?.UpdatedAt,
            HasResume = resume is not null,
            ResumeFileSize = resume == null ? null : resume.FileSize,
            ResumeUploadedAt = resume == null ? null : resume.UploadedAt
        });
    }

    private static string[] NormalizeEntries(string[] entries) => entries
        .Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
