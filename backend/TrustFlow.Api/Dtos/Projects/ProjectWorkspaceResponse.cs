using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Projects;

public class ProjectWorkspaceResponse
{
    public Guid Id { get; set; }
    public Guid? ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public Guid? FreelancerId { get; set; }
    public string? FreelancerFullName { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<MilestoneResponse> Milestones { get; set; } = [];
}