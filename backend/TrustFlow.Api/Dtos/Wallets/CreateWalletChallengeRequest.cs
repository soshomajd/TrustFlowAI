namespace TrustFlow.Api.Dtos.Wallets;

public sealed class CreateWalletChallengeRequest
{
    public string WalletAddress { get; set; } =
        string.Empty;
}