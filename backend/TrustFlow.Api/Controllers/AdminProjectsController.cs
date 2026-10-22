using System.Globalization;
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
[Route("api/admin/projects")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminProjectsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AdminProjectQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();

        var query = dbContext.Projects.AsNoTracking();
        if (request.Status.HasValue) query = query.Where(project => project.Status == request.Status.Value);
        if (request.ClientId.HasValue) query = query.Where(project => project.ClientId == request.ClientId.Value);
        if (request.FreelancerId.HasValue)
            query = query.Where(project => project.Milestones.Any(stage => stage.FreelancerId == request.FreelancerId.Value)
                || project.Escrows.Any(escrow => escrow.FreelancerId == request.FreelancerId.Value));
        if (request.From.HasValue) query = query.Where(project => project.CreatedAt >= request.From.Value);
        if (request.To.HasValue) query = query.Where(project => project.CreatedAt < request.To.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            var pattern = "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var isId = Guid.TryParse(search, out var projectId);
            query = query.Where(project => (isId && project.Id == projectId)
                || EF.Functions.ILike(project.Title, pattern, "\\")
                || (project.Client != null && EF.Functions.ILike(project.Client.FullName, pattern, "\\"))
                || project.Milestones.Any(stage => stage.Freelancer != null && EF.Functions.ILike(stage.Freelancer.FullName, pattern, "\\"))
                || project.Escrows.Any(escrow => dbContext.Users.Any(user => user.Id == escrow.FreelancerId
                    && EF.Functions.ILike(user.FullName, pattern, "\\"))));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(project => project.CreatedAt).ThenBy(project => project.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(project => new
            {
                project.Id, project.Title, project.Status, project.ClientId,
                ClientName = project.Client != null ? project.Client.FullName : "Former client",
                project.Budget, project.CreatedAt,
                MilestoneCount = project.Milestones.Count,
                EscrowCount = project.Escrows.Count,
                ProposalCount = project.Proposals.Count,
                OpenCaseCount = dbContext.EscrowCases.Count(item => item.Escrow.ProjectId == project.Id && item.Status == EscrowCaseStatus.Open)
            }).ToListAsync(cancellationToken);

        // Include current assignees and historical escrow parties. A cancelled
        // escrow can survive after its milestone assignments are cleared.
        var ids = rows.Select(row => row.Id).ToArray();
        var parties = await dbContext.Milestones.AsNoTracking()
            .Where(stage => ids.Contains(stage.ProjectId) && stage.FreelancerId != null)
            .Select(stage => new { stage.ProjectId, Id = stage.FreelancerId!.Value })
            .Union(dbContext.Escrows.AsNoTracking().Where(escrow => ids.Contains(escrow.ProjectId))
                .Select(escrow => new { escrow.ProjectId, Id = escrow.FreelancerId }))
            .Join(dbContext.Users, party => party.Id, user => user.Id,
                (party, user) => new { party.ProjectId, user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        var items = rows.Select(row => new AdminProjectSummaryResponse(row.Id, row.Title, row.Status,
            row.ClientId, row.ClientName, row.Budget.ToString(CultureInfo.InvariantCulture), row.CreatedAt,
            row.MilestoneCount, row.EscrowCount, row.ProposalCount, row.OpenCaseCount,
            parties.Where(party => party.ProjectId == row.Id).OrderBy(party => party.FullName).ThenBy(party => party.Id)
                .Select(party => new AdminProjectFreelancerResponse(party.Id, party.FullName)).ToList())).ToList();

        dbContext.AdminAccessLogs.Add(new AdminAccessLog { AdminId = adminId, Action = "AdminProjectsListed" });
        dbContext.AdminAccessLogs.AddRange(ids.Select(id => new AdminAccessLog
        {
            AdminId = adminId, ProjectId = id, Action = "AdminProjectListed"
        }));
        await dbContext.SaveChangesAsync(cancellationToken);

        Response.Headers.CacheControl = "private, no-store";
        var pages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Ok(new PagedResponse<AdminProjectSummaryResponse>
        {
            Items = items, Page = request.Page, PageSize = request.PageSize,
            TotalItems = total, TotalPages = pages,
            HasPreviousPage = request.Page > 1, HasNextPage = request.Page < pages
        });
    }
}
