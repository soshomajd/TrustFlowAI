namespace TrustFlow.Api.Models;

// Verified chain evidence, independent of off-chain approval/accounting.
// Index -1 is work start; each nonnegative index is one released/settled stage.
public sealed class EscrowExecutionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EscrowId { get; set; }
    public Escrow Escrow { get; set; } = null!;
    public Guid? MilestoneId { get; set; }
    public int MilestoneIndex { get; set; }
    public string Kind { get; set; } = "";
    public string TransactionHash { get; set; } = "";
    public string BlockHash { get; set; } = "";
    public long BlockNumber { get; set; }
    public int LogIndex { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string TransactionSender { get; set; } = "";
}
