using System.Numerics;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class EscrowFeePolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(9, 0)]
    [InlineData(10, 1)]
    [InlineData(11, 1)]
    [InlineData(19, 1)]
    [InlineData(101, 10)]
    [InlineData(333330000, 33333000)]
    public void AcceptsExactPerPayoutFloorAndRefusesEveryDifferentFee(long gross, long fee)
    {
        var payment = new OnChainMilestone(gross + 100, true, gross, false, fee);
        EscrowFeePolicy.ValidatePayment(payment, 1000);
        Assert.Throws<InvalidOperationException>(() => EscrowFeePolicy.ValidatePayment(
            payment with { PlatformFee = fee + 1 }, 1000));
        Assert.Throws<InvalidOperationException>(() => EscrowFeePolicy.ValidatePayment(
            payment with { PlatformFee = fee - 1 }, 1000));
    }

    [Fact]
    public void LegacyPolicyRejectsAnyFeeAndKeepsTheFullAward()
    {
        var payment = new OnChainMilestone(100, true, 100, false, 0);
        EscrowFeePolicy.ValidatePayment(payment, 0);
        Assert.Throws<InvalidOperationException>(() => EscrowFeePolicy.ValidatePayment(
            payment with { PlatformFee = 1 }, 0));
    }

    [Fact]
    public void PartialAwardsUseOnlyTheFreelancerGrossAndNeverTheClientRefund()
    {
        var settlement = new OnChainMilestone(101, true, 67, true, 6);
        EscrowFeePolicy.ValidatePayment(settlement, 1000);
        Assert.Throws<InvalidOperationException>(() => EscrowFeePolicy.ValidatePayment(
            settlement with { PlatformFee = 10 }, 1000));
        EscrowFeePolicy.ValidatePayment(settlement with { Paid = 0, PlatformFee = 0 }, 1000);
    }

    [Fact]
    public void LargeAwardsAreValidatedWithoutOverflowOrFloatingPoint()
    {
        var gross = BigInteger.Pow(2, 256) - 1;
        var fee = BigInteger.Parse("11579208923731619542357098500868790785326998466564056403945758400791312963993");
        EscrowFeePolicy.ValidatePayment(new OnChainMilestone(gross, true, gross, false, fee), 1000);
    }

    [Theory]
    [InlineData(-1, 0, 1000)]
    [InlineData(102, 10, 1000)]
    [InlineData(100, 10, 999)]
    public void InvalidAwardsOrPoliciesAreRefused(long gross, long fee, int policy)
    {
        Assert.Throws<InvalidOperationException>(() => EscrowFeePolicy.ValidatePayment(
            new OnChainMilestone(101, true, gross, false, fee), policy));
    }
}
