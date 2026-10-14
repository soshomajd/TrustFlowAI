using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models;

public class Escrow
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public Guid ProjectId { get; set; }

    [JsonIgnore]
    public Project Project { get; set; } =
        null!;

    // One escrow per freelancer on a project: the contract pays a single
    // immutable address, so each freelancer needs their own, covering only
    // the milestones assigned to them.
    public Guid FreelancerId { get; set; }

    public long ChainId { get; set; }

    public string TokenAddress { get; set; } =
        string.Empty;

    public string? ContractAddress { get; set; }

    public string? ClientWalletAddress { get; set; }

    public string? FreelancerWalletAddress { get; set; }

    // Who can settle a case on this contract. Fixed on-chain at deployment;
    // kept here so a later change of the server setting cannot make the
    // backend trust the wrong address for an escrow that already exists.
    public string? ArbiterWalletAddress { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal ReleasedAmount { get; set; }

    // Zero/null for existing V1 records. Fixed at creation for new V2 escrows.
    public int PlatformFeeBasisPoints { get; set; }
    public int? TokenDecimals { get; set; }
    public string? FeeRecipientAddress { get; set; }
    public decimal PlatformFeesPaid { get; set; }

    public EscrowStatus Status { get; set; } =
        EscrowStatus.PendingDeployment;

    public string? DeploymentTransactionHash { get; set; }

    public string? FundingTransactionHash
    {
        get;
        set;
    }

    public string? CancellationTransactionHash
    {
        get;
        set;
    }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset? DeployedAt { get; set; }

    public DateTimeOffset? FundedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    // The cancellation itself is the durable notification event. Only its
    // freelancer can acknowledge it; polling/sync must never reset this.
    public DateTimeOffset? CancellationNoticeReadAt { get; set; }
}
