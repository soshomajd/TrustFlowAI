using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Models
{
    public class MileStone
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = null!;
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public int SequenceNumber { get; set; }

        public DateTimeOffset Deadline { get; set; }

        public MileStoneStatus Status { get; set; } = MileStoneStatus.Pending;

        // Only set when the arbiter settled the milestone: what the
        // freelancer actually received for it.
        public decimal? PaidAmount { get; set; }

        // Set when the client accepts a proposal that covers this milestone.
        // Different milestones of one project can belong to different
        // freelancers, each paid from their own escrow.
        public Guid? FreelancerId { get; set; }

        [JsonIgnore]
        public ApplicationUser? Freelancer { get; set; }

        [JsonIgnore]
        public ICollection<ProposalMilestone> ProposalMilestones { get; set; } = [];

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;


    }
}