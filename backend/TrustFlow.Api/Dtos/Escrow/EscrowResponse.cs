using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Escrows;

public sealed class EscrowResponse
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    // A project can hold one escrow per assigned freelancer, so every
    // response says which freelancer this contract pays.
    public Guid FreelancerId { get; set; }

    public long ChainId { get; set; }

    public string TokenAddress { get; set; } =
        string.Empty;

    public string? ContractAddress { get; set; }

    public string? ClientWalletAddress { get; set; }

    public string? FreelancerWalletAddress
    {
        get;
        set;
    }

    public string? ArbiterWalletAddress { get; set; }

    public decimal TotalAmount { get; set; }

    public string TotalAmountExact => TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public decimal ReleasedAmount { get; set; }

    public string ReleasedAmountExact => ReleasedAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public int PlatformFeeBasisPoints { get; set; }
    public int? TokenDecimals { get; set; }
    public string? FeeRecipientAddress { get; set; }
    public decimal PlatformFeesPaid { get; set; }
    public string PlatformFeesPaidExact => PlatformFeesPaid.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public decimal FreelancerNetPaid { get; set; }
    public string FreelancerNetPaidExact => FreelancerNetPaid.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public EscrowStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
    public string? DeploymentTransactionHash
    {
        get;
        set;
    }

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

    public DateTimeOffset? DeployedAt
    {
        get;
        set;
    }

    public DateTimeOffset? FundedAt
    {
        get;
        set;
    }

    public DateTimeOffset? CompletedAt
    {
        get;
        set;
    }

    public DateTimeOffset? CancelledAt
    {
        get;
        set;
    }
}