using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Enums;

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

        public DateTime Deadline { get; set; }

        public MileStoneStatus Status { get; set; } = MileStoneStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    }
}