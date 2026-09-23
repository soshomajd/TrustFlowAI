
namespace TrustFlow.Api.Dtos.Milestones;

public class PublicMilestoneResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int SequenceNumber { get; set; }
    public DateTimeOffset Deadline { get; set; }

    // True once another freelancer's proposal for this stage was accepted,
    // so it can no longer be bid for.
    public bool IsTaken { get; set; }

}