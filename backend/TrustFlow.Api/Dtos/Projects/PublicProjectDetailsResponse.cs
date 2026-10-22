using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Projects;

public class PublicProjectDetailsResponse
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Budget { get; set; }
    public string BudgetExact => Budget.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal AllocatedAmount { get; set; }
    public string AllocatedAmountExact => AllocatedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal AvailableAmount { get; set; }
    public string AvailableAmountExact => AvailableAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public DateTimeOffset Deadline { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ProjectStatus Status { get; set; }
    public List<PublicMilestoneResponse> Milestones { get; set; } = [];
}
