
using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models
{
    public class Project
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? ClientId { get; set; }

        [JsonIgnore]
        public ApplicationUser? Client { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Budget { get; set; }

        public DateTimeOffset Deadline { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public ICollection<MileStone> Milestones { get; set; } = [];
        public ICollection<Proposal> Proposals { get; set; } = [];
        public Guid? FreelancerId { get; set; }
        [JsonIgnore]
        public ApplicationUser? Freelancer { get; set; }
        public ProjectStatus Status { get; set; } = ProjectStatus.Open;
    }
}