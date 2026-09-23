using System.Text.Json.Serialization;

namespace TrustFlow.Api.Models;

// A proposal covers one or more of a project's milestones. A freelancer can
// bid for a single stage, a few of them, or all of them.
public class ProposalMilestone
{
    public Guid ProposalId { get; set; }

    [JsonIgnore]
    public Proposal Proposal { get; set; } = null!;

    public Guid MilestoneId { get; set; }

    [JsonIgnore]
    public MileStone Milestone { get; set; } = null!;
}
