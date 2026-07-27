using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Proposals;

public class ProposalResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FreelancerId { get; set; }
    public string CoverLetter { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public int EstimatedDays { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}