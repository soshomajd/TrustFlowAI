"use client";

import { Building2, Check, Download, Inbox, LoaderCircle, RefreshCw, ShieldCheck, UserRound, X } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { downloadClientVerificationDocument } from "@/features/clients/api/clients-api";
import { usePendingClientVerifications, useReviewClientVerification } from "@/features/clients/hooks/use-client-profile";
import type { ClientVerificationAdminItem } from "@/features/clients/types/client-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function AdminClientVerificationsScreen() {
  const query = usePendingClientVerifications();
  return <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    <header className="rounded-2xl border bg-card/80 p-6 sm:p-8"><div className="flex size-12 items-center justify-center rounded-xl bg-primary/10"><ShieldCheck className="size-6 text-electric" /></div><p className="mt-6 text-xs font-medium uppercase tracking-[0.2em] text-electric">Admin review</p><h1 className="mt-3 text-3xl font-semibold">Client identity queue</h1><p className="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">Compare each PDF with the submitted legal details. The sensitive document is removed automatically after your decision.</p></header>
    {query.isPending ? <div className="mt-6 h-64 animate-pulse rounded-2xl border bg-muted/20" /> : query.isError ? <section className="mt-6 rounded-2xl border border-destructive/30 bg-destructive/5 p-6"><p className="text-sm text-destructive">{getApiErrorMessage(query.error)}</p><Button variant="outline" className="mt-4" onClick={() => void query.refetch()}><RefreshCw className="size-4" />Try again</Button></section> : query.data.length === 0 ? <section className="mt-6 rounded-2xl border border-dashed p-10 text-center"><Inbox className="mx-auto size-10 text-muted-foreground" /><h2 className="mt-4 text-lg font-semibold">Queue is clear</h2><p className="mt-2 text-sm text-muted-foreground">There are no client documents waiting for review.</p></section> : <div className="mt-6 space-y-4">{query.data.map((item) => <ReviewCard key={item.clientId} item={item} />)}</div>}
  </main>;
}

function ReviewCard({ item }: { item: ClientVerificationAdminItem }) {
  const mutation = useReviewClientVerification();
  const [reason, setReason] = useState("");
  const [downloadError, setDownloadError] = useState("");
  const Icon = item.accountType === "Company" ? Building2 : UserRound;
  const rejectDisabled = mutation.isPending || !reason.trim();

  async function download() { setDownloadError(""); try { await downloadClientVerificationDocument(item.clientId); } catch (error) { setDownloadError(getApiErrorMessage(error, "Document download failed.")); } }

  return <article className="rounded-2xl border bg-card/70 p-5 sm:p-6">
    <div className="flex flex-col gap-5 lg:flex-row lg:justify-between">
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-3"><div className="flex size-10 items-center justify-center rounded-xl bg-primary/10"><Icon className="size-5 text-electric" /></div><div><h2 className="break-words font-semibold">{item.displayName}</h2><p className="break-all text-xs text-muted-foreground">{item.email}</p></div></div>
        <dl className="mt-5 grid gap-4 text-sm sm:grid-cols-2">
          <Detail label="Account type" value={item.accountType} /><Detail label="Legal name" value={item.legalName} /><Detail label="Issuing country" value={item.countryCode} />{item.accountType === "Company" && <Detail label="Registration number" value={item.registrationNumber} />}<Detail label="File size" value={formatBytes(item.documentFileSize)} /><Detail label="Submitted" value={formatDate(item.submittedAt)} />
        </dl>
      </div>
      <Button type="button" variant="outline" onClick={() => void download()} className="self-start"><Download className="size-4" />Download private PDF</Button>
    </div>
    {downloadError && <p role="alert" className="mt-4 text-sm text-destructive">{downloadError}</p>}
    <div className="mt-6 grid gap-4 border-t pt-5 lg:grid-cols-[minmax(0,1fr)_auto] lg:items-end">
      <div className="space-y-2"><Label htmlFor={`reason-${item.clientId}`}>Rejection reason</Label><Textarea id={`reason-${item.clientId}`} value={reason} onChange={(event) => setReason(event.target.value)} maxLength={300} rows={2} placeholder="Required only when rejecting. Tell the client exactly what to fix." /></div>
      <div className="flex flex-wrap gap-2"><Button type="button" variant="destructive" disabled={rejectDisabled} onClick={() => mutation.mutate({ clientId: item.clientId, approve: false, reason: reason.trim() })}>{mutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <X className="size-4" />}Reject</Button><Button type="button" disabled={mutation.isPending} onClick={() => mutation.mutate({ clientId: item.clientId, approve: true, reason: "" })}>{mutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Check className="size-4" />}Approve</Button></div>
    </div>
    {mutation.isError && <p role="alert" className="mt-4 rounded-xl border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{getApiErrorMessage(mutation.error, "The decision could not be saved.")}</p>}
  </article>;
}

function Detail({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words font-medium">{value || "Not provided"}</dd></div>; }
function formatBytes(value: number) { return `${(value / 1024 / 1024).toFixed(2)} MB`; }
function formatDate(value: string) { return new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value)); }
