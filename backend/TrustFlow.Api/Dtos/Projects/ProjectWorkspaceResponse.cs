using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;

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

    // Pending plus accepted only, matching the API rule: the workspace
    // hides project and milestone editing while a proposal is still live,
    // and shows it again once they are all rejected or withdrawn.
    public int ActiveProposalCount { get; set; }
    public List<MilestoneResponse> Milestones { get; set; } = [];
}