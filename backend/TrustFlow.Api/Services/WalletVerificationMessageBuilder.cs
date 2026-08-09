namespace TrustFlow.Api.Services;

public static class WalletVerificationMessageBuilder
{
    public static string Build(
        Guid userId,
        string walletAddress,
        string nonce,
        DateTimeOffset expiresAt)
    {
        return string.Join(
            "\n",
            new[]
            {
                "TrustFlow AI Wallet Verification",
                string.Empty,
                $"User ID: {userId:D}",
                $"Wallet Address: {walletAddress}",
                $"Nonce: {nonce}",
                $"Expires At: {expiresAt:O}",
                string.Empty,
                "Sign this message to prove wallet ownership.",
                "This action does not create a blockchain transaction or cost gas."
            }
        );
    }
}