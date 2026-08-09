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

    public long ChainId { get; set; }

    public string TokenAddress { get; set; } =
        string.Empty;

    public string? ContractAddress { get; set; }

    public string? ClientWalletAddress { get; set; }

    public string? FreelancerWalletAddress { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal ReleasedAmount { get; set; }

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
}