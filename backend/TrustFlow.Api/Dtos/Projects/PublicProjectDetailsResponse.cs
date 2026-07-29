using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Projects;

public class PublicProjectDetailsResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal AllocatedAmount { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ProjectStatus Status { get; set; }
    public List<PublicMilestoneResponse> Milestones { get; set; } = [];
}