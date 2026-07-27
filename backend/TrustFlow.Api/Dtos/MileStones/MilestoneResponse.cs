
using TrustFlow.Api.Models.Enums;

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
}