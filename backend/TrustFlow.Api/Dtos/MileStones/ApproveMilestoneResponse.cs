using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Milestones;

public class ApproveMilestoneResponse
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public MileStoneStatus Status { get; set; }

    public ProjectStatus ProjectStatus { get; set; }
}