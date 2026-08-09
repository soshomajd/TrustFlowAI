namespace TrustFlow.Api.Dtos.Projects;

public sealed class FreelancerDashboardSummaryResponse
{
    public int TotalProposals { get; set; }

    public int PendingProposals { get; set; }

    public int AssignedProjects { get; set; }

    public int RejectedMilestones { get; set; }
}