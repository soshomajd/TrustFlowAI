namespace TrustFlow.Api.Dtos.Wallets;

public sealed class VerifyWalletRequest
{
    public string Signature { get; set; } =
        string.Empty;
}