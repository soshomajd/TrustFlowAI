using System.Numerics;

namespace TrustFlow.Api.Blockchain;

public static class TokenAmountConverter
{
    public static BigInteger ToBaseUnits(
        decimal amount,
        int tokenDecimals)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Amount must not be negative."
            );
        }

        if (tokenDecimals < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenDecimals),
                tokenDecimals,
                "Token decimals must not be negative."
            );
        }

        var bits = decimal.GetBits(amount);

        var scale = (bits[3] >> 16) & 0x7F;

        var unscaledValue =
            (new BigInteger((uint)bits[2]) << 64) |
            (new BigInteger((uint)bits[1]) << 32) |
            new BigInteger((uint)bits[0]);

        if (scale > tokenDecimals)
        {
            var divisor = BigInteger.Pow(
                10,
                scale - tokenDecimals
            );

            var remainder = unscaledValue % divisor;

            if (remainder != BigInteger.Zero)
            {
                throw new ArgumentException(
                    $"Amount {amount} cannot be represented " +
                    $"exactly with {tokenDecimals} token decimals.",
                    nameof(amount)
                );
            }

            return unscaledValue / divisor;
        }

        var multiplier = BigInteger.Pow(
            10,
            tokenDecimals - scale
        );

        return unscaledValue * multiplier;
    }
}
