using System.ComponentModel.DataAnnotations;
using System.Globalization;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.Dtos.Admin;

public sealed class AdminDisputeQuery : AdminPageQuery
{
    [RegularExpression("^(escrow|written)$")]
    public string Kind { get; init; } = "escrow";
}

public sealed record AdminDisputeSummary(Guid Id, string RecordType, Guid MilestoneId, string MilestoneTitle,
    decimal MilestoneAmount, string Status, string OpenedByRole, Guid? ClientId, string ClientName,
    Guid? FreelancerId, string FreelancerName, DateTimeOffset OpenedAt, DateTimeOffset? ClosedAt)
{
    public string? ReferenceCode => RecordType == "escrow" ? TrustFlow.Api.Models.EscrowCaseReference.Format(Id) : null;
    public string? CaseKind { get; init; }
    public string MilestoneAmountExact => MilestoneAmount.ToString(CultureInfo.InvariantCulture);
    public DateTimeOffset? ReviewableAt => RecordType == "escrow" ? Add(EscrowCaseRules.ResponseWindow) : null;
    public DateTimeOffset? FallbackAt => RecordType == "escrow" ? Add(EscrowCaseRules.StallWindow) : null;
    private DateTimeOffset? Add(TimeSpan period) => OpenedAt <= DateTimeOffset.MaxValue - period ? OpenedAt + period : null;
}

public sealed record AdminSavedOpinion(bool Readable, string? Summary, string? Reasoning,
    int? FreelancerSharePercent, string? Verdict, DateTimeOffset? RecordedAt, bool? IsDemo);

public sealed record AdminDisputeDetail(AdminDisputeSummary Record, Guid? EscrowId, long? ChainId,
    string? ContractAddress, string? TokenAddress, string? ArbiterWalletAddress, string? ClientWalletAddress,
    string? FreelancerWalletAddress, string? OpenerStatement, string? RespondentStatement, DateTimeOffset? RespondentStatementAt,
    string? ReviewNote, DateTimeOffset? ReviewNoteAt, AdminFinanceSettlement? Outcome, AdminSavedOpinion? Opinion);
