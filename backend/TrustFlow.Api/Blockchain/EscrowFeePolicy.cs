using System.Numerics;

namespace TrustFlow.Api.Blockchain;

public static class EscrowFeePolicy
{
    public const int BasisPoints = 1000;

    public static void ValidatePayment(OnChainMilestone payment, int basisPoints)
    {
        var expected = basisPoints switch
        {
            0 => BigInteger.Zero,
            BasisPoints => payment.Paid / 10,
            _ => throw new InvalidOperationException("Unsupported escrow fee policy.")
        };
        if (payment.PlatformFee != expected || payment.Paid < 0 || payment.Paid > payment.Amount)
            throw new InvalidOperationException("On-chain gross award and platform fee do not match the escrow policy.");
    }
}
