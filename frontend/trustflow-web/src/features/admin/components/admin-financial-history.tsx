"use client";
import { useState, type ReactNode } from "react";
import { downloadAdminFinanceReport } from "@/features/admin/api/admin-finance-api";
import { Button } from "@/components/ui/button";
import { useAdminFinance, useAdminFinanceEntries } from "@/features/admin/hooks/use-admin-finance";
import type { FinancePayout, FinanceSettlement, FinanceSection } from "@/features/admin/types/admin-finance";
import { formatMoney } from "@/lib/money/exact-money";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
const money = (value: string | null) => value === null ? "Unknown / not recorded" : formatMoney(value);
const date = (value: string | null) => value ? new Date(value).toLocaleString() : "Not recorded";
function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-all">{children ?? "Not recorded"}</dd></div>;
}
function Entry({ row }: { row: FinancePayout | FinanceSettlement }) {
  return <article className="rounded-xl border bg-card/60 p-5">
    <h4 className="font-semibold">{row.kind === "payout" ? row.title : row.milestoneTitle}</h4>
    <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
      <Field label="Record ID">{row.id}</Field><Field label="Gross freelancer award">{money(row.grossAmountExact)}</Field>
      <Field label="Recorded platform fee">{money(row.platformFeePaidExact)}</Field><Field label="Net received by freelancer">{money(row.freelancerNetPaidExact)}</Field>
      {row.kind === "payout" ? <>
        <Field label="Saved milestone status">{row.status}</Field><Field label="Settlement case">{row.caseId ?? (row.status === "Settled" ? "Not recorded" : "Not applicable")}</Field>
        <Field label="Recorded settlement time">{row.caseId || row.status === "Settled" ? date(row.recordedAt) : "Not applicable"}</Field>
      </> : <>
        <Field label="Returned to client">{money(row.returnedToClientExact)}</Field><Field label="Escrow ended">{row.escrowEnded ? "Yes" : "No"}</Field>
        <Field label="Recorded closure time">{date(row.closedAt)}</Field><Field label="Milestone ID">{row.milestoneId}</Field>
      </>}
      <Field label="Payment transaction hash">{row.paymentTransactionHash ?? "Not recorded"}</Field>
      <Field label="Payment block time">{date(row.paymentAt ?? null)}</Field>
    </dl>
  </article>;
}
export function AdminFinancialHistory({ projectId }: { projectId: string }) {
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState<string>();
  const [page, setPage] = useState(1);
  const [chosen, setChosen] = useState<string>();
  const [section, setSection] = useState<FinanceSection>("payouts");
  const [entryPage, setEntryPage] = useState(1);
  const overview = useAdminFinance(projectId, page);
  const selected = overview.data?.items.find((item) => item.id === chosen) ?? overview.data?.items[0];
  const entries = useAdminFinanceEntries(projectId, selected?.id, section, entryPage);
  async function downloadReport() {
    if (!selected) return;
    setDownloading(true); setDownloadError(undefined);
    try {
      const report = await downloadAdminFinanceReport(projectId, selected.id);
      const url = URL.createObjectURL(report);
      try {
        const anchor = document.createElement("a"); anchor.href = url;
        anchor.download = `trustflow-finance-${projectId}-${selected.id}.csv`; anchor.click();
      } finally { URL.revokeObjectURL(url); }
    } catch { setDownloadError("The report could not be downloaded. Try again. Very large reports may need the paginated history instead."); }
    finally { setDownloading(false); }
  }
  return <section aria-label="Financial history" className="space-y-5">
    <div className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-semibold">Financial history</h2>
      <Button variant="outline" disabled={overview.isFetching || (Boolean(selected) && entries.isFetching)} onClick={() => { void overview.refetch(); if (selected) void entries.refetch(); }}>Refresh financial records</Button></div>
    <p className="rounded-xl border p-4 text-sm text-muted-foreground">Saved accounting records, not live wallet balances. Each escrow keeps its own network and token; amounts are not combined across contracts. Case outcomes describe payouts already included in the escrow totals. Verified payment hashes and block times are shown when available; missing historical evidence stays Not recorded.</p>
    <aside aria-label="Financial records privacy" className="rounded-xl border p-4 text-sm text-muted-foreground">
      <p>Confidential project financial records. Administrator views and report downloads are recorded. Share reports only with authorized people and store them securely.</p>
      <p className="mt-2">Each CSV report contains all saved financial entries for the selected escrow, not just this page. Import its value column as Text to preserve exact decimals and long identifiers.</p>
    </aside>
    {overview.isPending && <p role="status">Loading financial records…</p>}
    {overview.isError && <div role="alert" className="rounded-xl border border-destructive/40 p-4"><p>Could not load financial history.</p><p>{getApiErrorMessage(overview.error, "Try Refresh financial records.")}</p>{overview.data && <p>The records below are from the last successful refresh.</p>}</div>}
    {overview.data && <>
      {overview.data.items.length === 0 && <p className="rounded-xl border border-dashed p-5">No escrow financial records on this page.</p>}
      <div className="grid gap-4 lg:grid-cols-2">{overview.data.items.map((item) => <article key={item.id} className={`rounded-2xl border bg-card/60 p-5 ${selected?.id === item.id ? "border-primary/60" : ""}`}>
        <h3 className="font-semibold">{item.freelancerName} · {item.status}</h3>
        <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2">
          <Field label="Escrow ID">{item.id}</Field><Field label="Chain ID">{item.chainId}</Field>
          <Field label="Token address">{item.tokenAddress}</Field><Field label="Token decimals">{item.tokenDecimals}</Field>
          <Field label="Contract address">{item.contractAddress}</Field><Field label="Contract total">{money(item.totalAmountExact)}</Field>
          <Field label="Funding supported by saved evidence">{money(item.fundingAmountExact)}</Field>
          <Field label="Gross released">{money(item.grossReleasedExact)}</Field><Field label="Platform fees paid">{money(item.platformFeesPaidExact)}</Field>
          <Field label="Net paid to freelancer">{money(item.freelancerNetPaidExact)}</Field><Field label="Case refunds recorded">{money(item.recordedCaseRefundsExact)}</Field>
          <Field label="Full cancellation refund">{item.status === "Cancelled" ? money(item.cancellationRefundExact) : "Not applicable"}</Field><Field label="Remainder from saved records">{money(item.remainingRecordedAmountExact)}</Field>
          <Field label="Funding recorded">{date(item.fundedAt)}</Field><Field label="Cancellation recorded">{item.status === "Cancelled" ? date(item.cancelledAt) : "Not applicable"}</Field>
          <Field label="Last saved update">{date(item.updatedAt)}</Field>
          <Field label="Work-start block time">{date(item.workStartedAt ?? null)}</Field>
        </dl>
        {item.gaps.length > 0 && <ul aria-label="Missing or inconsistent financial records" className="mt-4 list-disc space-y-2 rounded-lg border border-amber-500/30 p-4 pl-8 text-sm">{item.gaps.map((gap) => <li key={gap}>{gap}</li>)}</ul>}
        <details className="mt-4 rounded-lg border p-3"><summary className="cursor-pointer text-sm">Saved transaction references</summary>
          <dl className="mt-4 space-y-4 text-sm"><Field label="Deployment">{item.deploymentTransactionHash}</Field><Field label="Funding">{item.fundingTransactionHash}</Field><Field label="Cancellation">{item.cancellationTransactionHash}</Field><Field label="Work start">{item.workStartTransactionHash ?? "Not recorded"}</Field></dl>
        </details>
        <Button variant="outline" className="mt-4" aria-pressed={selected?.id === item.id} onClick={() => { setChosen(item.id); setEntryPage(1); }}>View entries for this escrow</Button>
      </article>)}</div>
      <nav aria-label="Financial escrow pages" className="flex items-center justify-between gap-3">
        <Button variant="outline" disabled={!overview.data.hasPreviousPage || overview.isFetching} onClick={() => { setPage((old) => old - 1); setChosen(undefined); setEntryPage(1); }}>Previous escrows</Button>
        <p>Page {overview.data.page} of {Math.max(1, overview.data.totalPages)}</p>
        <Button variant="outline" disabled={!overview.data.hasNextPage || overview.isFetching} onClick={() => { setPage((old) => old + 1); setChosen(undefined); setEntryPage(1); }}>Next escrows</Button>
      </nav>
    </>}
    {selected && <section className="space-y-4" aria-label="Selected escrow financial entries">
      <div className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-lg font-semibold">Entries for {selected.freelancerName}</h3>
        <Button variant="outline" disabled={downloading} onClick={() => void downloadReport()}>{downloading ? "Downloading report…" : "Download selected escrow report (CSV)"}</Button></div>
      {downloadError && <p role="alert" className="text-sm text-destructive">{downloadError}</p>}<p className="break-all text-xs text-muted-foreground">Escrow {selected.id} · Chain {selected.chainId} · Token {selected.tokenAddress}</p>
      <nav aria-label="Financial entry sections" className="flex flex-wrap gap-2">
        <Button variant={section === "payouts" ? "default" : "outline"} aria-pressed={section === "payouts"} onClick={() => { setSection("payouts"); setEntryPage(1); }}>Milestone payouts</Button>
        <Button variant={section === "settlements" ? "default" : "outline"} aria-pressed={section === "settlements"} onClick={() => { setSection("settlements"); setEntryPage(1); }}>Settled cases and refunds</Button>
      </nav>
      {entries.isPending && <p role="status">Loading financial entries…</p>}
      {entries.isError && <div role="alert"><p>Could not load financial entries.</p>{entries.data && <p>The entries below are from the last successful refresh.</p>}<Button variant="outline" onClick={() => void entries.refetch()}>Retry entries</Button></div>}
      {entries.data && <>
        {entries.data.items.length ? entries.data.items.map((row) => <Entry row={row} key={row.id} />) : <p className="rounded-xl border border-dashed p-5">No recorded entries on this page.</p>}
        <nav aria-label="Financial entry pages" className="flex items-center justify-between gap-3">
          <Button variant="outline" disabled={!entries.data.hasPreviousPage || entries.isFetching} onClick={() => setEntryPage((old) => old - 1)}>Previous entries</Button>
          <p>Page {entries.data.page} of {Math.max(1, entries.data.totalPages)}</p>
          <Button variant="outline" disabled={!entries.data.hasNextPage || entries.isFetching} onClick={() => setEntryPage((old) => old + 1)}>Next entries</Button>
        </nav>
      </>}
    </section>}
  </section>;
}
