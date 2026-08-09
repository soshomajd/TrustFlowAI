using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Escrows;

public sealed class EscrowResponse
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

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

    public decimal TotalAmount { get; set; }

    public decimal ReleasedAmount { get; set; }

    public EscrowStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}