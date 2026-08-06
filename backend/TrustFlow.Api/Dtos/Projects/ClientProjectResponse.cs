using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Projects;

public sealed class ClientProjectResponse
{
    public Guid Id { get; set; }

    public Guid? FreelancerId { get; set; }

    public string? FreelancerFullName { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public decimal Budget { get; set; }

    public decimal AllocatedAmount { get; set; }

    public int MilestoneCount { get; set; }

    public int ApprovedMilestoneCount { get; set; }

    public int ProposalCount { get; set; }

    public int PendingProposalCount { get; set; }

    public DateTimeOffset Deadline { get; set; }

    public ProjectStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    
    public int SubmittedMilestoneCount { get; set; }
}