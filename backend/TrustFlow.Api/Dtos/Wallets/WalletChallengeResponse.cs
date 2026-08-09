namespace TrustFlow.Api.Dtos.Wallets;

public sealed class WalletChallengeResponse
{
    public string WalletAddress { get; set; } =
        string.Empty;

    public string Message { get; set; } =
        string.Empty;

    public DateTimeOffset ExpiresAt
    {
        get;
        set;
    }
}