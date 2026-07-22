using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Models
{
    public class Proposal
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProjectId { get; set; }
        [JsonIgnore]
        public Project Project { get; set; } = null!;
        public Guid FreelancerId { get; set; }
        [JsonIgnore]
        public ApplicationUser Freelancer { get; set; } = null!;
        public string CoverLetter { get; set; } = string.Empty;
        public decimal BidAmount { get; set; }
        public int EstimatedDays { get; set; }
        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;




    }
}