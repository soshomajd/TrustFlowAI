
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Projects;
using TrustFlow.Api.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TrustFlow.Api.Constants;

namespace TrustFlow.Api.Controllers
{
    [ApiController]
    [Route("api/projects")]
    public class ProjectsController(AppDbContext dbContext) : ControllerBase
    {
        [HttpPost]
        [Authorize(Roles = AppRoles.Client)]
        public async Task<IActionResult> CreateProject(CreateProjectRequest request, CancellationToken cancellationToken)
        {
            var clientIdValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(clientIdValue, out var clientId))
            {
                return Unauthorized(new
                {
                    message = "Invalid user identity."
                });
            }
            var project = new Project
            {
                ClientId = clientId,
                Title = request.Title,
                Description = request.Description,
                Budget = request.Budget,
                Deadline = request.DeadLine
            };

            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Created($"/api/projects/{project.Id}", project);
        }

        [HttpGet]
        public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
        {
            var projects = await dbContext.Projects
                .AsNoTracking()
                .OrderByDescending(project => project.CreatedAt)
                .ToListAsync(cancellationToken); return Ok(projects);
        }
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetProfilById(Guid id, CancellationToken cancellationToken)
        {
            var project = await dbContext.Projects
                .AsNoTracking()
                .Include(project =>
                    project.Milestones.OrderBy(milestone => milestone.SequenceNumber)
                )
                .FirstOrDefaultAsync(
                    project => project.Id == id,
                    cancellationToken
                ); if (project is null)
            {
                return NotFound(new
                {
                    message = "Project Not Found"
                });
            }
            return Ok(project);
        }
        [HttpPut("{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]
        public async Task<IActionResult> UpdateProject(Guid id, UpdateProjectRequest updateProjectRequest, CancellationToken cancellationToken)
        {

            var clientIdValue = User.FindFirstValue(
                 ClaimTypes.NameIdentifier
            );

            if (!Guid.TryParse(clientIdValue, out var clientId))
            {
                return Unauthorized();
            }

            var project = await dbContext.Projects.FirstOrDefaultAsync(project => project.Id == id && project.ClientId == clientId, cancellationToken);
            if (project is null)
            {
                return NotFound(new
                {
                    message = "Project not found."
                });
            }
            project.Title = updateProjectRequest.Title;
            project.Description = updateProjectRequest.Description;
            project.Budget = updateProjectRequest.Budget;
            project.Deadline = updateProjectRequest.Deadline;

            await dbContext.SaveChangesAsync(cancellationToken);

            return Ok(project);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = AppRoles.Client)]

        public async Task<IActionResult> DeleteProject(Guid id, CancellationToken cancellationToken)
        {
            var ClientValueId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );
            if (!Guid.TryParse(ClientValueId, out var clientId))
            {
                return Unauthorized();
            }
            var project = await dbContext.Projects.FirstOrDefaultAsync(project => project.Id == id && project.ClientId == clientId, cancellationToken);
            if (project is null)
            {
                return NotFound(new
                {
                    message = "Project not found."
                });
            }

            dbContext.Projects.Remove(project);
            await dbContext.SaveChangesAsync(cancellationToken);
            return NoContent();
        }



    }
}