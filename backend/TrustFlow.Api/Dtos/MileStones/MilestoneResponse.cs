
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Milestones;

public class MilestoneResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string AmountExact => Amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public int SequenceNumber { get; set; }

    public DateTimeOffset Deadline { get; set; }
    public MileStoneStatus Status { get; set; }

    // Only set when the reviewer settled the milestone: what the freelancer
    // received for it, which may be less than Amount.
    public decimal? PaidAmount { get; set; }
    public string? PaidAmountExact => PaidAmount?.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal PlatformFeePaid { get; set; }
    public string PlatformFeePaidExact => PlatformFeePaid.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal? FreelancerNetPaid { get; set; }
    public string? FreelancerNetPaidExact => FreelancerNetPaid?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    // Milestones are assigned one freelancer at a time, and each freelancer
    // is paid by their own escrow, so the caller needs to know whose stage
    // this is before it can release payment for it.
    public Guid? FreelancerId { get; set; }
    public string? FreelancerFullName { get; set; }
}