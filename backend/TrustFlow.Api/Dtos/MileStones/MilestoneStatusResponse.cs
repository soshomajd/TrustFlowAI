using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Milestones;

public class MilestoneStatusResponse
{
    public Guid Id { get; set; }

    public MileStoneStatus Status { get; set; }
}