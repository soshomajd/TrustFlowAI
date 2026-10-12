namespace TrustFlow.Api.Blockchain;

// The timings fixed in the escrow contract, repeated here so the screens can
// show countdowns. The contract enforces them; changing these changes nothing
// on-chain.
public static class EscrowCaseRules
{
    // How long the other side has before the arbiter may settle a case.
    public static readonly TimeSpan ResponseWindow = TimeSpan.FromDays(14);

    // How long the arbiter may stay silent before either side can force a
    // 50/50 split and end the escrow.
    public static readonly TimeSpan StallWindow = TimeSpan.FromDays(90);
}
