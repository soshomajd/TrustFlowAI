namespace TrustFlow.Api.Dtos.Projects;

public sealed class ClientDashboardSummaryResponse
{
    public int TotalProjects { get; set; }
    public int OpenProjects { get; set; }
    public int InProgressProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int PendingProposals { get; set; }
    public decimal TotalBudget { get; set; }
}