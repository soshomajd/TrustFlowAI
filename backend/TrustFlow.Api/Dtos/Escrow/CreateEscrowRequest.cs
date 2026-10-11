using System.ComponentModel.DataAnnotations;
namespace TrustFlow.Api.Dtos.Escrows;

public sealed class CreateEscrowRequest
{
    // Which freelancer on the project this escrow pays. Each freelancer gets
    // their own contract covering only their milestones. It may be omitted
    // only while the project has a single assigned freelancer, matching how
    // the read, deploy and sync routes resolve an escrow.
    public Guid? FreelancerId { get; set; }

    public long ChainId { get; set; }

    public string TokenAddress { get; set; } =
        string.Empty;
}