namespace TrustFlow.Api.Dtos.Wallets;

public sealed class WalletVerificationResponse
{
    public string WalletAddress { get; set; } =
        string.Empty;

    public DateTimeOffset VerifiedAt
    {
        get;
        set;
    }
}