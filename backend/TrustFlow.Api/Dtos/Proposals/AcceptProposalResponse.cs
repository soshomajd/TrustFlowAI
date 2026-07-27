using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Proposals;

public class AcceptProposalResponse
{
    public Guid ProposalId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid FreelancerId { get; set; }
    public ProposalStatus ProposalStatus { get; set; }
    public ProjectStatus ProjectStatus { get; set; }
}