using System.Globalization;
using System.Numerics;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class TokenAmountConverterTests
{
    [Theory]
    [InlineData("90071992547409.91", 6, "90071992547409910000")]
    [InlineData("35184372088832.01", 6, "35184372088832010000")]
    [InlineData("9999999999999999.99", 18, "9999999999999999990000000000000000")]
    [InlineData("0.000000000000000001", 18, "1")]
    [InlineData("1.2300000", 6, "1230000")]
    [InlineData("12.000", 0, "12")]
    [InlineData("0", 18, "0")]
    public void ConvertsExactAmountsInBothDirections(string amount, int decimals, string units)
    {
        var expected = decimal.Parse(amount, CultureInfo.InvariantCulture);
        var baseUnits = BigInteger.Parse(units, CultureInfo.InvariantCulture);
        Assert.Equal(baseUnits, TokenAmountConverter.ToBaseUnits(expected, decimals));
        Assert.Equal(expected, TokenAmountConverter.FromBaseUnits(baseUnits, decimals));
    }

    [Fact]
    public void LargeBaseUnitsAreAllowedWhenTheTokenAmountFitsDecimalExactly()
    {
        var units = new BigInteger(decimal.MaxValue) * BigInteger.Pow(10, 18);
        Assert.True(units > new BigInteger(decimal.MaxValue));
        Assert.Equal(decimal.MaxValue, TokenAmountConverter.FromBaseUnits(units, 18));
        Assert.Equal(units, TokenAmountConverter.ToBaseUnits(decimal.MaxValue, 18));
    }

    [Theory]
    [InlineData("0.0000001", 6)]
    [InlineData("1.23456789", 6)]
    [InlineData("1.01", 0)]
    public void ExtraNonZeroDigitsAreRejectedRatherThanRounded(string amount, int decimals)
    {
        Assert.Throws<ArgumentException>(() => TokenAmountConverter.ToBaseUnits(
            decimal.Parse(amount, CultureInfo.InvariantCulture), decimals));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(19)]
    [InlineData(255)]
    public void UnsupportedPrecisionIsRejectedInBothDirections(int decimals)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenAmountConverter.ToBaseUnits(1m, decimals));
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenAmountConverter.FromBaseUnits(BigInteger.One, decimals));
    }

    [Fact]
    public void NegativeOverflowingAndInexactAmountsAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenAmountConverter.ToBaseUnits(-1m, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenAmountConverter.FromBaseUnits(-BigInteger.One, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenAmountConverter.FromBaseUnits(new BigInteger(decimal.MaxValue) + 1, 0));
        var inexact = BigInteger.Parse("1000000000000000000000000000001", CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(() => TokenAmountConverter.FromBaseUnits(inexact, 18));
    }

    [Fact]
    public void ConversionDoesNotDependOnTheServersCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Assert.Equal(123456.78m, TokenAmountConverter.FromBaseUnits(new BigInteger(123456780000), 6));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }
}
