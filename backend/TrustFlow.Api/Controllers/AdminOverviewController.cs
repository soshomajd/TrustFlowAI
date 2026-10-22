using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/admin/overview")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminOverviewController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();

        // Aggregate in the database; the landing page never receives projects,
        // participant details, conversations, wallet secrets or file contents.
        var counts = await dbContext.Projects.AsNoTracking()
            .GroupBy(project => project.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Status, row => row.Count, cancellationToken);

        var totalEscrows = await dbContext.Escrows.CountAsync(cancellationToken);
        var openCases = await dbContext.EscrowCases.CountAsync(
            item => item.Status == EscrowCaseStatus.Open, cancellationToken);

        var response = new AdminOverviewResponse(
            counts.Values.Sum(),
            counts.GetValueOrDefault(ProjectStatus.Open),
            counts.GetValueOrDefault(ProjectStatus.InProgress),
            counts.GetValueOrDefault(ProjectStatus.Completed),
            counts.GetValueOrDefault(ProjectStatus.Cancelled),
            totalEscrows, openCases, DateTimeOffset.UtcNow);

        // Fail closed if the durable access record cannot be stored. No
        // project, escrow or participant workflow is changed by this endpoint.
        dbContext.AdminAccessLogs.Add(new AdminAccessLog
        {
            AdminId = adminId,
            Action = "AdminOverviewViewed"
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        Response.Headers.CacheControl = "private, no-store";
        return Ok(response);
    }
}
