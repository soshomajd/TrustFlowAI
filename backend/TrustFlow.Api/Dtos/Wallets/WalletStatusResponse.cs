namespace TrustFlow.Api.Dtos.Wallets;

public sealed class WalletStatusResponse
{
    public string? WalletAddress { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public bool IsVerified { get; set; }
}
