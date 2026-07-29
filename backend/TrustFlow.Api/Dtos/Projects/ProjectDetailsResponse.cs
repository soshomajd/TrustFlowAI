using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Projects;

public class ProjectDetailsResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }

    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<MilestoneResponse> Milestones { get; set; } = [];
}