using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Proposals;

public class MyProposalResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectTitle { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public int EstimatedDays { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}