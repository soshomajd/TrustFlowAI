using System.Globalization;
using System.Text;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Admin;

public static class AdminFinanceCsv
{
    // Quoting protects delimiters; the apostrophe also neutralizes spreadsheet formulas,
    // including ones hidden behind leading whitespace/control characters.
    private static string Cell(string? value)
    {
        value ??= "Unknown / not recorded";
        var first = value.TrimStart().FirstOrDefault();
        if (first is '=' or '+' or '-' or '@' || value.StartsWith('\t') || value.StartsWith('\r') || value.StartsWith('\n'))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static byte[] Create(Guid projectId, string projectTitle, AdminFinanceEscrow escrow,
        IReadOnlyList<AdminFinancePayout> payouts, IReadOnlyList<AdminFinanceSettlement> settlements, DateTimeOffset generatedAt)
    {
        var csv = new StringBuilder();
        void Row(params string?[] cells) => csv.AppendJoin(',', cells.Select(Cell)).Append("\r\n");
        string Time(DateTimeOffset? date) => date?.ToString("O", CultureInfo.InvariantCulture) ?? "Not recorded";
        // A field/value layout keeps the exact saved decimal text and missing-value markers
        // explicit without pretending that summary/payout/case rows can be summed together.
        Row("record_type", "project_id", "escrow_id", "record_id", "field", "value");
        void Field(string type, Guid id, string key, string? value) => Row(type, projectId.ToString(), escrow.Id.ToString(), id.ToString(), key, value);
        void Summary(string key, string? value) => Field("escrow_summary", escrow.Id, key, value);
        Summary("project_title", projectTitle);
        Summary("report_generated_at", Time(generatedAt));
        Summary("report_scope", "One escrow; all saved milestone payouts and settled cases, not just the displayed page.");
        Summary("privacy", "Confidential project financial records. Administrator exports are recorded. Share only with authorized people and store securely.");
        Summary("accounting_note", "Saved accounting records, not live balances. Case outcomes describe awards already included in gross totals; do not sum sections together.");
        Summary("spreadsheet_note", "Import the value column as Text to preserve exact decimals and long identifiers. Formula-like text has a protective apostrophe.");
        Summary("status", escrow.Status.ToString());
        Summary("freelancer_id", escrow.FreelancerId.ToString()); Summary("freelancer_name", escrow.FreelancerName);
        Summary("chain_id", escrow.ChainId.ToString(CultureInfo.InvariantCulture)); Summary("token_address", escrow.TokenAddress);
        Summary("token_decimals", escrow.TokenDecimals?.ToString(CultureInfo.InvariantCulture)); Summary("contract_address", escrow.ContractAddress);
        Summary("contract_total", escrow.TotalAmountExact); Summary("funding_supported_by_saved_evidence", escrow.FundingAmountExact);
        Summary("gross_released", escrow.GrossReleasedExact); Summary("platform_fees_paid", escrow.PlatformFeesPaidExact);
        Summary("freelancer_net_paid", escrow.FreelancerNetPaidExact); Summary("case_refunds_recorded", escrow.RecordedCaseRefundsExact);
        Summary("full_cancellation_refund", escrow.Status == EscrowStatus.Cancelled ? escrow.CancellationRefundExact : "Not applicable");
        Summary("remainder_from_saved_records", escrow.RemainingRecordedAmountExact);
        Summary("deployment_transaction_hash", escrow.DeploymentTransactionHash); Summary("funding_transaction_hash", escrow.FundingTransactionHash);
        Summary("cancellation_transaction_hash", escrow.CancellationTransactionHash); Summary("funding_recorded_at", Time(escrow.FundedAt));
        Summary("cancellation_recorded_at", Time(escrow.CancelledAt)); Summary("last_saved_update", Time(escrow.UpdatedAt));
        Summary("traceability_note", "Milestone payment transaction hashes and ordinary client-release timestamps are not stored. Case times are recorded closure times.");
        foreach (var gap in escrow.Gaps) Summary("missing_or_inconsistent_record", gap);
        foreach (var payout in payouts)
        {
            void Payout(string key, string? value) => Field("milestone_payout", payout.Id, key, value);
            Payout("title", payout.Title); Payout("status", payout.Status.ToString()); Payout("gross_freelancer_award", payout.GrossAmountExact);
            Payout("recorded_platform_fee", payout.PlatformFeePaidExact); Payout("freelancer_net_paid", payout.FreelancerNetPaidExact);
            Payout("settlement_case_id", payout.CaseId?.ToString()); Payout("recorded_settlement_time", Time(payout.RecordedAt));
            Payout("payment_transaction_hash", "Not recorded");
        }
        foreach (var outcome in settlements)
        {
            void Outcome(string key, string? value) => Field("settled_case", outcome.Id, key, value);
            Outcome("milestone_id", outcome.MilestoneId.ToString()); Outcome("milestone_title", outcome.MilestoneTitle);
            Outcome("gross_freelancer_award", outcome.GrossAmountExact); Outcome("recorded_platform_fee", outcome.PlatformFeePaidExact);
            Outcome("freelancer_net_paid", outcome.FreelancerNetPaidExact); Outcome("returned_to_client", outcome.ReturnedToClientExact);
            Outcome("escrow_ended", outcome.EscrowEnded ? "Yes" : "No"); Outcome("recorded_closure_time", Time(outcome.ClosedAt));
            Outcome("payment_transaction_hash", "Not recorded");
        }
        // BOM makes UTF-8 names readable in common spreadsheet importers.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}
