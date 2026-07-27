namespace TrustFlow.Api.Dtos.Projects;

public class ProjectSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public decimal AllocatedAmount { get; set; }
    public int MilestoneCount { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}