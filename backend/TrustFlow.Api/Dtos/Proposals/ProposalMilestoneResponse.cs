namespace TrustFlow.Api.Dtos.Proposals;

// A proposal covers the milestones the freelancer chose to deliver, and its
// price is their total. Both sides need to see which stages those are: the
// client to know what they are agreeing to, and the freelancer to see what
// they applied for.
public sealed class ProposalMilestoneResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public int SequenceNumber { get; set; }
}
