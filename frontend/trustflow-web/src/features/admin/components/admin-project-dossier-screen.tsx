"use client";

import { useState, type ReactNode } from "react";
import Link from "next/link";
import axios from "axios";
import { Button } from "@/components/ui/button";
import { useAdminDossier, useAdminDossierRecords } from "@/features/admin/hooks/use-admin-dossier";
import type { DossierRow, DossierSection } from "@/features/admin/types/admin-dossier";
import { formatMoney } from "@/lib/money/exact-money";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { AdminDeliveryCard } from "@/features/admin/components/admin-delivery-card";
import { AdminDisputes } from "@/features/admin/components/admin-disputes";
import { AdminConversations } from "@/features/admin/components/admin-conversations";
import { AdminFinancialHistory } from "@/features/admin/components/admin-financial-history";

const sections: { key: DossierSection; label: string }[] = [
  { key: "participants", label: "Participants" }, { key: "milestones", label: "Milestones" },
  { key: "proposals", label: "Proposals" }, { key: "escrows", label: "Escrow contracts" },
  { key: "deliveries", label: "Deliveries" },
  { key: "finance", label: "Financial history" },
  { key: "conversations", label: "Conversations" },
  { key: "disputes", label: "Disputes" },
];
const date = (value: string) => new Date(value).toLocaleString();
const status = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");
function Field({ label, children }: { label: string; children: ReactNode }) {
  return <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-all">{children ?? "Not recorded"}</dd></div>;
}

function RecordCard({ row, projectId }: { row: DossierRow; projectId: string }) {
  if (row.kind === "delivery") return <AdminDeliveryCard delivery={row} projectId={projectId} />;
  if (row.kind === "participant") return <article className="rounded-xl border bg-card/60 p-5">
    <h3 className="font-semibold wrap-anywhere">{row.fullName}</h3>
    <p className="mt-1 text-sm text-muted-foreground">{[
      row.isClient && "Client", row.currentFreelancer && "Current freelancer",
      row.hasEscrow && (row.currentFreelancer ? "Escrow participant" : "Past escrow participant"),
      row.hasProposal && "Proposal author",
      row.hasDelivery && "Delivery author",
    ].filter(Boolean).join(" · ")}</p>
    <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2"><Field label="Account ID">{row.id}</Field>
      <Field label="Current account wallet">{row.walletAddress ?? "No wallet saved"}</Field>
      <Field label="Wallet verification">{row.walletVerified ? "Verified" : "Not verified"}</Field></dl>
  </article>;
  if (row.kind === "milestone") return <article className="rounded-xl border bg-card/60 p-5">
    <h3 className="font-semibold wrap-anywhere">{row.sequenceNumber}. {row.title}</h3>
    <p className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm text-muted-foreground">{row.description}</p>
    <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
      <Field label="Status">{status(row.status)}</Field><Field label="Amount">{formatMoney(row.amountExact)}</Field>
      <Field label="Deadline">{date(row.deadline)}</Field><Field label="Assigned freelancer">{row.freelancerName ?? "Unassigned"}</Field>
      <Field label="Freelancer ID">{row.freelancerId ?? "Unassigned"}</Field><Field label="Milestone ID">{row.id}</Field>
    </dl>
  </article>;
  if (row.kind === "proposal") return <article className="rounded-xl border bg-card/60 p-5">
    <h3 className="font-semibold">{row.freelancerName}</h3>
    <p className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm">{row.coverLetter}</p>
    <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
      <Field label="Status">{status(row.status)}</Field><Field label="Proposal amount">{formatMoney(row.bidAmountExact)}</Field>
      <Field label="Requested milestones">{row.milestoneCount}</Field><Field label="Estimated time">{row.estimatedDays} days</Field>
      <Field label="Submitted">{date(row.createdAt)}</Field><Field label="Proposal ID">{row.id}</Field>
      <Field label="Freelancer ID">{row.freelancerId}</Field>
    </dl>
  </article>;
  return <article className="rounded-xl border bg-card/60 p-5">
    <h3 className="font-semibold">{row.freelancerName} · {status(row.status)}</h3>
    <dl className="mt-4 grid gap-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
      <Field label="Escrow ID">{row.id}</Field><Field label="Freelancer ID">{row.freelancerId}</Field>
      <Field label="Chain ID">{row.chainId}</Field><Field label="Contract address">{row.contractAddress ?? "Not deployed"}</Field>
      <Field label="Token address">{row.tokenAddress}</Field><Field label="Token decimals">{row.tokenDecimals}</Field>
      <Field label="Contract total">{formatMoney(row.totalAmountExact)}</Field><Field label="Gross released">{formatMoney(row.releasedAmountExact)}</Field>
      <Field label="Freelancer fee">{row.platformFeeBasisPoints / 100}%</Field>
      <Field label="Contract client wallet">{row.clientWalletAddress}</Field><Field label="Contract freelancer wallet">{row.freelancerWalletAddress}</Field>
      <Field label="Arbiter wallet">{row.arbiterWalletAddress}</Field><Field label="Fee receiving wallet">{row.feeRecipientAddress}</Field>
      <Field label="Created">{date(row.createdAt)}</Field><Field label="Last saved update">{date(row.updatedAt)}</Field>
    </dl>
  </article>;
}

export function AdminProjectDossierScreen({ projectId }: { projectId: string }) {
  const [section, setSection] = useState<DossierSection>("participants");
  const [page, setPage] = useState(1);
  const project = useAdminDossier(projectId);
  const records = useAdminDossierRecords(projectId, section, page, Boolean(project.data) && section !== "finance" && section !== "conversations" && section !== "disputes");
  const notFound = axios.isAxiosError(project.error) && project.error.response?.status === 404;
  const data = project.data;
  return <main className="mx-auto max-w-7xl space-y-6 px-4 py-8 sm:px-6">
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div><Link href="/dashboard/admin/projects" className="text-sm text-electric hover:underline">All projects</Link>
        <p className="mt-4 text-xs uppercase tracking-widest text-electric">Project dossier</p>
        <h1 className="mt-2 text-3xl font-semibold wrap-anywhere">{data?.title ?? "Project record"}</h1>
      </div>
      <Button variant="outline" disabled={project.isFetching || (Boolean(data) && section !== "finance" && section !== "conversations" && section !== "disputes" && records.isFetching)} onClick={() => { void project.refetch(); if (data && section !== "finance" && section !== "conversations" && section !== "disputes") void records.refetch(); }}>Refresh dossier</Button>
    </header>
    {project.isPending && <p role="status">Loading project dossier…</p>}
    {project.isError && <div role="alert" className="rounded-xl border border-destructive/40 p-5">
      <p className="font-medium">{notFound ? "Project not found" : "Could not load project dossier"}</p>
      <p className="mt-2 text-sm">{notFound ? "This project is not available. Return to All projects." : getApiErrorMessage(project.error, "Try Refresh dossier.")}</p>
      {data && <p className="mt-2 text-sm">The details below are from the last successful refresh.</p>}
    </div>}
    {data && <>
      <section aria-label="Project details" className="rounded-2xl border bg-card/60 p-5">
        <p className="whitespace-pre-wrap wrap-anywhere text-sm leading-6">{data.description}</p>
        <dl className="mt-6 grid gap-5 text-sm sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Project ID">{data.id}</Field><Field label="Status">{status(data.status)}</Field>
          <Field label="Project budget">{formatMoney(data.budgetExact)}</Field><Field label="Client">{data.clientName}</Field>
          <Field label="Client ID">{data.clientId ?? "No client account saved"}</Field><Field label="Deadline">{date(data.deadline)}</Field>
          <Field label="Created">{date(data.createdAt)}</Field><Field label="Participants">{data.participantCount}</Field>
          <Field label="Records">{data.milestoneCount} milestones · {data.proposalCount} proposals · {data.escrowCount} escrows · {data.deliveryCount ?? 0} deliveries</Field>
        </dl>
      </section>
      <p className="rounded-xl border p-4 text-sm text-muted-foreground">Read-only project records. Views are recorded. Current account wallets can differ from wallets fixed into an older contract. Contract information is the last saved record, not a fresh blockchain check.</p>
      <nav aria-label="Dossier sections" className="flex flex-wrap gap-2">{sections.map((item) =>
        <Button key={item.key} variant={section === item.key ? "default" : "outline"} aria-pressed={section === item.key} onClick={() => { setSection(item.key); setPage(1); }}>{item.label}</Button>)}</nav>
      {section === "finance" ? <AdminFinancialHistory key={projectId} projectId={projectId} /> : section === "conversations" ? <AdminConversations key={projectId} projectId={projectId} /> : section === "disputes" ? <AdminDisputes key={projectId} projectId={projectId} /> : <section aria-label={sections.find((item) => item.key === section)?.label} className="space-y-4">
        {records.isPending && <p role="status">Loading records…</p>}
        {records.isError && <div role="alert" className="rounded-xl border border-destructive/40 p-5"><p>Could not load these records.</p>
          <p className="mt-2 text-sm">{getApiErrorMessage(records.error, "Try loading these records again.")}</p>
          {records.data && <p className="mt-2 text-sm">The records below are from the last successful refresh.</p>}
          <Button className="mt-4" variant="outline" onClick={() => void records.refetch()}>Retry records</Button>
        </div>}
        {records.data && <>
          <p className="text-sm text-muted-foreground">{records.data.totalItems} matching records</p>
          {records.data.items.length ? records.data.items.map((row) => <RecordCard key={row.id} row={row} projectId={projectId} />) : <p className="rounded-xl border border-dashed p-6">No records on this page.</p>}
          <nav aria-label="Record pages" className="flex items-center justify-between gap-3">
            <Button variant="outline" disabled={!records.data.hasPreviousPage || records.isFetching} onClick={() => setPage((old) => old - 1)}>Previous</Button>
            <p className="text-sm">Page {records.data.page} of {Math.max(1, records.data.totalPages)}</p>
            <Button variant="outline" disabled={!records.data.hasNextPage || records.isFetching} onClick={() => setPage((old) => old + 1)}>Next</Button>
          </nav>
        </>}
      </section>}
    </>}
  </main>;
}
