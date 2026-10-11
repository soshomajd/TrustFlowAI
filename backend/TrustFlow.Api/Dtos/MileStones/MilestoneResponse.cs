
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
    public int SequenceNumber { get; set; }

    public DateTimeOffset Deadline { get; set; }
    public MileStoneStatus Status { get; set; }

    // Milestones are assigned one freelancer at a time, and each freelancer
    // is paid by their own escrow, so the caller needs to know whose stage
    // this is before it can release payment for it.
    public Guid? FreelancerId { get; set; }
    public string? FreelancerFullName { get; set; }
}