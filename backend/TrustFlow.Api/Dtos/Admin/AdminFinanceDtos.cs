using System.Globalization;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Admin;

public sealed class AdminFinanceEscrow
{
    public Guid Id { get; init; }
    public Guid FreelancerId { get; init; }
    public string FreelancerName { get; init; } = string.Empty;
    public EscrowStatus Status { get; init; }
    public long ChainId { get; init; }
    public string TokenAddress { get; init; } = string.Empty;
    public string? ContractAddress { get; init; }
    public int? TokenDecimals { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal GrossReleased { get; init; }
    public decimal PlatformFeesPaid { get; init; }
    public decimal RecordedCaseRefunds { get; init; }
    public int UnknownCaseRefundCount { get; init; }
    public decimal RecordedMilestoneGross { get; init; }
    public decimal RecordedMilestoneFees { get; init; }
    public int UnknownPayoutAmountCount { get; init; }
    public string? DeploymentTransactionHash { get; init; }
    public string? FundingTransactionHash { get; init; }
    public string? CancellationTransactionHash { get; init; }
    public DateTimeOffset? FundedAt { get; init; }
    public DateTimeOffset? CancelledAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? WorkStartedAt { get; init; }
    public string? WorkStartTransactionHash { get; init; }
    private bool HasFunding => FundedAt.HasValue || !string.IsNullOrWhiteSpace(FundingTransactionHash);
    private bool ValidAmounts => TotalAmount >= 0 && GrossReleased >= 0 && GrossReleased <= TotalAmount
        && PlatformFeesPaid >= 0 && PlatformFeesPaid <= GrossReleased
        && RecordedCaseRefunds >= 0 && RecordedCaseRefunds <= TotalAmount - GrossReleased;
    private decimal? CancellationRefund => ValidAmounts && Status == EscrowStatus.Cancelled && HasFunding
        && !string.IsNullOrWhiteSpace(CancellationTransactionHash) && GrossReleased == 0
        && PlatformFeesPaid == 0 && RecordedCaseRefunds == 0 ? TotalAmount : null;
    private decimal? Remaining => !ValidAmounts || UnknownCaseRefundCount > 0 || !HasFunding ? null
        : Status == EscrowStatus.Funded ? TotalAmount - GrossReleased - RecordedCaseRefunds
        : Status == EscrowStatus.Completed && TotalAmount == GrossReleased + RecordedCaseRefunds ? 0
        : CancellationRefund.HasValue ? 0 : null;
    private static string? Exact(decimal? value) => value?.ToString(CultureInfo.InvariantCulture);
    public string TotalAmountExact => Exact(TotalAmount)!;
    public string? FundingAmountExact => Exact(HasFunding ? TotalAmount : null);
    public string GrossReleasedExact => Exact(GrossReleased)!;
    public string PlatformFeesPaidExact => Exact(PlatformFeesPaid)!;
    public string? FreelancerNetPaidExact => Exact(ValidAmounts ? GrossReleased - PlatformFeesPaid : null);
    public string RecordedCaseRefundsExact => Exact(RecordedCaseRefunds)!;
    public string? CancellationRefundExact => Exact(CancellationRefund);
    public string? RemainingRecordedAmountExact => Exact(Remaining);
    public IReadOnlyList<string> Gaps
    {
        get
        {
            var result = new List<string>();
            if (!ValidAmounts) result.Add("Saved amounts do not reconcile; net and remaining amounts are unavailable.");
            if (UnknownCaseRefundCount > 0) result.Add("Some settled-case refund amounts are not recorded.");
            if (UnknownPayoutAmountCount > 0) result.Add("Some milestone payout amounts are not recorded.");
            if (RecordedMilestoneGross != GrossReleased || RecordedMilestoneFees != PlatformFeesPaid)
                result.Add("Saved milestone payouts do not match the escrow totals.");
            if (Status == EscrowStatus.Terminated) result.Add("Non-case termination refunds are not stored individually; the remaining amount is unknown.");
            if (Status == EscrowStatus.Cancelled && !CancellationRefund.HasValue)
                result.Add("The cancellation refund amount cannot be established from the saved lifecycle records.");
            if (!HasFunding) result.Add("Funding evidence is not recorded; the contract total is not proof of a deposit.");
            if (Status == EscrowStatus.Completed && Remaining is null)
                result.Add("The completed escrow does not fully reconcile from its saved gross payments and case refunds.");
            return result;
        }
    }
}

public sealed record AdminFinancePayout(Guid Id, string Title, MileStoneStatus Status, decimal? GrossAmount,
    decimal PlatformFeePaid, Guid? CaseId, DateTimeOffset? RecordedAt,
    string? PaymentTransactionHash = null, DateTimeOffset? PaymentAt = null)
{
    public string Kind => "payout";
    public string? GrossAmountExact => GrossAmount?.ToString(CultureInfo.InvariantCulture);
    public string? PlatformFeePaidExact => GrossAmount.HasValue && PlatformFeePaid >= 0 && PlatformFeePaid <= GrossAmount
        ? PlatformFeePaid.ToString(CultureInfo.InvariantCulture) : null;
    public string? FreelancerNetPaidExact => PlatformFeePaidExact is not null
        ? (GrossAmount!.Value - PlatformFeePaid).ToString(CultureInfo.InvariantCulture) : null;
}

public sealed record AdminFinanceSettlement(Guid Id, Guid MilestoneId, string MilestoneTitle, decimal? GrossAmount,
    decimal? PlatformFeePaid, decimal? ReturnedToClient, bool EscrowEnded, DateTimeOffset? ClosedAt,
    string? PaymentTransactionHash = null, DateTimeOffset? PaymentAt = null)
{
    public string Kind => "settlement";
    public string? GrossAmountExact => GrossAmount?.ToString(CultureInfo.InvariantCulture);
    public string? PlatformFeePaidExact => PlatformFeePaid?.ToString(CultureInfo.InvariantCulture);
    public string? FreelancerNetPaidExact => GrossAmount.HasValue && PlatformFeePaid.HasValue && PlatformFeePaid >= 0 && PlatformFeePaid <= GrossAmount
        ? (GrossAmount.Value - PlatformFeePaid.Value).ToString(CultureInfo.InvariantCulture) : null;
    public string? ReturnedToClientExact => ReturnedToClient?.ToString(CultureInfo.InvariantCulture);
}
