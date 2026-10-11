namespace TrustFlow.Api.Dtos.Escrows;

// One row per freelancer who holds milestones on the project. The row exists
// as soon as milestones are assigned, with a null escrow until the client
// creates one, so the client workspace can show what still needs setting up.
public sealed class ProjectEscrowParticipantResponse
{
    public Guid FreelancerId { get; set; }

    public string FreelancerFullName { get; set; } =
        string.Empty;

    public int MilestoneCount { get; set; }

    // The sum of this freelancer's milestones, which is what their escrow
    // must hold rather than the whole project budget.
    public decimal AssignedTotal { get; set; }

    public EscrowResponse? Escrow { get; set; }
}
