using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;
namespace TrustFlow.Api.Dtos.Projects;

public class ProjectSummaryResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string BudgetExact => Budget.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal AllocatedAmount { get; set; }
    public string AllocatedAmountExact => AllocatedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal AvailableAmount { get; set; }
    public string AvailableAmountExact => AvailableAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public int MilestoneCount { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ProjectStatus Status { get; set; }

}
