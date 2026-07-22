
using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Identity;

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

        public DateTime Deadline { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<MileStone> Milestones { get; set; } = [];
        public ICollection<Proposal> Proposals { get; set; } = [];
    }
}