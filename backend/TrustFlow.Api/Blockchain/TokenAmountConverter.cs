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

    // The exact inverse of ToBaseUnits. An amount that does not fit a
    // decimal, or loses digits on the way, is an error, never a rounding.
    public static decimal FromBaseUnits(
        BigInteger baseUnits,
        int tokenDecimals)
    {
        if (baseUnits.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseUnits),
                baseUnits,
                "Amount must not be negative."
            );
        }

        if (tokenDecimals < 0 || tokenDecimals > 18)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokenDecimals),
                tokenDecimals,
                "Token decimals must be between 0 and 18."
            );
        }

        if (baseUnits > new BigInteger(decimal.MaxValue))
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseUnits),
                baseUnits,
                "Amount is too large."
            );
        }

        var divisor = 1m;

        for (var i = 0; i < tokenDecimals; i++)
        {
            divisor *= 10m;
        }

        var amount = (decimal)baseUnits / divisor;

        if (ToBaseUnits(amount, tokenDecimals) != baseUnits)
        {
            throw new ArgumentException(
                $"Amount {baseUnits} cannot be represented exactly.",
                nameof(baseUnits)
            );
        }

        return amount;
    }
}
