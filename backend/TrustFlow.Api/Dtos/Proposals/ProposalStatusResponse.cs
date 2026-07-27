using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Proposals;

public class ProposalStatusResponse
{
    public Guid Id { get; set; }

    public ProposalStatus Status { get; set; }
}