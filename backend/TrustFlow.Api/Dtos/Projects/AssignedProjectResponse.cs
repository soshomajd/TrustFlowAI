using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Projects;

public class AssignedProjectResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal AllocatedAmount { get; set; }
    public int MilestoneCount { get; set; }
    public int RejectedMilestoneCount { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public ProjectStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}