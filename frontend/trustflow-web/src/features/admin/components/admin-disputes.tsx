"use client";
import { useState, type ReactNode } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { useAdminDisputes, useAdminDispute } from "@/features/admin/hooks/use-admin-disputes";
import type { AdminDisputeKind } from "@/features/admin/types/admin-disputes";
import { formatMoney } from "@/lib/money/exact-money";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
const date = (value: string | null) => value ? new Date(value).toLocaleString() : "Not recorded";
const money = (value: string | null) => value === null ? "Unknown / not recorded" : formatMoney(value);
function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 whitespace-pre-wrap wrap-anywhere">{children ?? "Not recorded"}</dd></div>;
}
export function AdminDisputeDetails({ projectId, kind, id }: { projectId: string; kind: AdminDisputeKind; id: string }) {
  const query = useAdminDispute(projectId, kind, id);
  const data = query.data?.record.id === id && query.data.record.recordType === kind ? query.data : undefined;
  return <section aria-label="Selected dispute" className="space-y-4 rounded-2xl border p-5">
    <header className="flex flex-wrap items-center justify-between gap-3"><h3 className="text-lg font-semibold">Dispute details</h3>
      <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>Refresh saved dispute</Button></header>
    {query.isPending && <p role="status">Loading dispute details…</p>}
    {query.isError && <div role="alert"><p>Could not load this dispute.</p><p>{getApiErrorMessage(query.error, "Try Refresh saved dispute.")}</p>{data && <p>The details below are from the last successful refresh.</p>}</div>}
    {data && <>
      <dl className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
        {data.record.referenceCode && <Field label="Case reference">{data.record.referenceCode}</Field>}<Field label="Record ID">{data.record.id}</Field><Field label="Milestone">{data.record.milestoneTitle}</Field>
        <Field label="Saved status">{data.record.status}</Field><Field label="Opened by">{data.record.openedByRole}</Field>
        <Field label="Client">{data.record.clientName}</Field><Field label="Freelancer">{data.record.freelancerName}</Field>
        <Field label="Client account">{data.record.clientId}</Field><Field label="Freelancer account">{data.record.freelancerId}</Field>
        <Field label="Milestone ID">{data.record.milestoneId}</Field><Field label="Milestone amount">{money(data.record.milestoneAmountExact)}</Field>
        <Field label="Opened">{date(data.record.openedAt)}</Field><Field label={kind === "escrow" ? "Recorded closure" : "Opinion recorded"}>{date(data.record.closedAt)}</Field>
      </dl>
      {kind === "escrow" ? <>
        <p className="rounded-lg border p-4 text-sm text-muted-foreground">Timings come from the recorded case opening. Reaching a date does not confirm current on-chain eligibility. Arbiter settlements use the existing reviewer desk and the human arbiter’s wallet.</p>
        <dl className="grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Arbiter review from (14 days)">{date(data.record.reviewableAt)}</Field><Field label="Silence fallback from (90 days)">{date(data.record.fallbackAt)}</Field>
          <Field label="Case request">{data.record.caseKind}</Field><Field label="Escrow ID">{data.escrowId}</Field><Field label="Chain ID">{data.chainId}</Field><Field label="Contract address">{data.contractAddress}</Field>
          <Field label="Token address">{data.tokenAddress}</Field><Field label="Immutable arbiter wallet">{data.arbiterWalletAddress}</Field>
          <Field label="Contract client wallet">{data.clientWalletAddress}</Field><Field label="Contract freelancer wallet">{data.freelancerWalletAddress}</Field>
        </dl>
        <div className="rounded-lg border p-4"><h4 className="font-semibold">Recorded reviewer note</h4><p dir="auto" className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm">{data.reviewNote ?? "No reviewer note recorded."}</p><p className="mt-2 text-xs text-muted-foreground">{date(data.reviewNoteAt)}</p></div>
        {data.outcome ? <div className="rounded-lg border p-4"><h4 className="font-semibold">Recorded settlement outcome</h4>
          <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2"><Field label="Gross freelancer award">{money(data.outcome.grossAmountExact)}</Field>
            <Field label="Platform fee">{money(data.outcome.platformFeePaidExact)}</Field><Field label="Freelancer net">{money(data.outcome.freelancerNetPaidExact)}</Field>
            <Field label="Returned to client">{money(data.outcome.returnedToClientExact)}</Field><Field label="Escrow ended">{data.outcome.escrowEnded ? "Yes" : "No"}</Field><Field label="Payment transaction hash">{data.outcome.paymentTransactionHash}</Field><Field label="Payment block time">{date(data.outcome.paymentAt ?? null)}</Field></dl>
        </div> : <p className="text-sm text-muted-foreground">No settled financial outcome is recorded for this case.</p>}
        <p className="text-sm text-muted-foreground">Written disagreements are listed separately. No direct case-to-statement link is stored, so statements are not automatically attached to this case.</p>
        <Link href={`/dashboard/review/${encodeURIComponent(data.record.id)}`} className="text-sm text-electric underline">Review financial decision</Link>
      </> : <div className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-lg border p-4"><h4 className="font-semibold">Opener’s statement</h4><p dir="auto" className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm">{data.openerStatement ?? "Not recorded"}</p></div>
        <div className="rounded-lg border p-4"><h4 className="font-semibold">Respondent’s statement</h4><p dir="auto" className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm">{data.respondentStatement ?? "No response recorded."}</p><p className="mt-2 text-xs text-muted-foreground">{date(data.respondentStatementAt)}</p></div>
      </div>}
      <section aria-label="Saved AI opinion" className="space-y-3 rounded-lg border p-4"><h4 className="font-semibold">Saved AI opinion · advice only</h4>
        <p className="text-sm text-muted-foreground">An AI opinion does not settle the contract or transfer funds. This view does not request a new opinion.</p>
        {data.opinion ? <>
          <p className="text-sm">{data.opinion.isDemo === true ? "Demo opinion (Mock provider)" : data.opinion.isDemo === false ? "Saved model opinion" : "Original AI provider is not recorded."}</p>
          {data.opinion.readable ? <><p dir="auto" className="whitespace-pre-wrap wrap-anywhere text-sm">{data.opinion.summary ?? "Summary not recorded."}</p>
            <p dir="auto" className="whitespace-pre-wrap wrap-anywhere text-sm text-muted-foreground">{data.opinion.reasoning ?? "Reasoning not recorded."}</p></> : <p className="text-sm text-amber-300">The saved AI text could not be read. Financial records are unchanged.</p>}
          <dl className="grid gap-4 text-sm sm:grid-cols-3"><Field label="Suggested freelancer share">{data.opinion.freelancerSharePercent === null ? "Not recorded" : `${data.opinion.freelancerSharePercent}%`}</Field>
            <Field label="Saved opinion label">{data.opinion.verdict}</Field><Field label="Opinion saved">{date(data.opinion.recordedAt)}</Field></dl>
        </> : <p className="text-sm">No saved AI opinion.</p>}
      </section>
      <p className="text-sm text-muted-foreground">Use Deliveries and Conversations in this project dossier to inspect the saved work and correspondence.</p>
    </>}
  </section>;
}

export function AdminDisputes({ projectId }: { projectId: string }) {
  const [kind, setKind] = useState<AdminDisputeKind>("escrow"); const [page, setPage] = useState(1); const [selectedId, setSelectedId] = useState<string>();
  const query = useAdminDisputes(projectId, kind, page);
  const selected = query.data?.items.find((item) => item.id === selectedId);
  return <section aria-label="Project disputes" className="space-y-5">
    <header className="flex flex-wrap items-center justify-between gap-3"><h2 className="text-xl font-semibold">Disputes</h2><Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>Refresh dispute list</Button></header>
    <p className="rounded-xl border p-4 text-sm text-muted-foreground">Confidential, read-only saved records. Administrator access is recorded. Escrow cases and written disagreements with AI opinions are separate records. Their statuses do not mean the same thing. Opening this view does not sync the chain or change a case.</p>
    <nav aria-label="Dispute record types" className="flex flex-wrap gap-2">{(["escrow", "written"] as const).map((value) => <Button key={value} variant={kind === value ? "default" : "outline"} aria-pressed={kind === value} onClick={() => { setKind(value); setPage(1); setSelectedId(undefined); }}>{value === "escrow" ? "Escrow cases" : "Written disagreements"}</Button>)}</nav>
    {query.isPending && <p role="status">Loading disputes…</p>}
    {query.isError && <div role="alert"><p>Could not load disputes.</p><p>{getApiErrorMessage(query.error, "Try Refresh dispute list.")}</p>{query.data && <p>The list below is from the last successful refresh.</p>}</div>}
    {query.data && <>
      <p className="text-sm text-muted-foreground">{query.data.totalItems} saved records</p>
      {query.data.items.length ? <ul className="grid gap-4 lg:grid-cols-2">{query.data.items.map((item) => <li key={item.id} className="space-y-3 rounded-xl border bg-card/60 p-5">
        <h3 className="font-semibold wrap-anywhere">{item.milestoneTitle}</h3><p className="text-sm">{item.status} · Opened by {item.openedByRole}</p>
        <p className="text-sm wrap-anywhere">{item.clientName} and {item.freelancerName} · {money(item.milestoneAmountExact)}</p><p className="text-xs text-muted-foreground">Opened {date(item.openedAt)}</p>
        <Button variant="outline" aria-pressed={selectedId === item.id} onClick={() => setSelectedId(item.id)}>View dispute details</Button>
      </li>)}</ul> : <p className="rounded-lg border border-dashed p-5">No saved records of this type on this page.</p>}
      <nav aria-label="Dispute pages" className="flex items-center justify-between gap-3"><Button variant="outline" disabled={!query.data.hasPreviousPage || query.isFetching} onClick={() => { setSelectedId(undefined); setPage((old) => old - 1); }}>Previous disputes</Button>
        <p className="text-sm">Page {query.data.page} of {Math.max(1, query.data.totalPages)}</p><Button variant="outline" disabled={!query.data.hasNextPage || query.isFetching} onClick={() => { setSelectedId(undefined); setPage((old) => old + 1); }}>Next disputes</Button></nav>
      {selected && <AdminDisputeDetails key={`${kind}-${selected.id}`} projectId={projectId} kind={kind} id={selected.id} />}
    </>}
  </section>;
}
