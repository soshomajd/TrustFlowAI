"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { DossierDelivery } from "@/features/admin/types/admin-dossier";
import { downloadAdminDeliveryFile } from "@/features/admin/api/admin-dossier-api";
import { safeDeliveryUrl } from "@/features/admin/lib/safe-delivery-url";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function AdminDeliveryCard({ delivery, projectId }: { delivery: DossierDelivery; projectId: string }) {
  const [downloading, setDownloading] = useState<string | null>(null);
  const [error, setError] = useState<string>();
  const preview = safeDeliveryUrl(delivery.previewLink);
  const source = safeDeliveryUrl(delivery.sourceFilesLink);
  async function download(id: string, name: string) {
    setDownloading(id); setError(undefined);
    try {
      const file = await downloadAdminDeliveryFile(projectId, delivery.id, id);
      const url = URL.createObjectURL(file);
      try {
        const anchor = document.createElement("a"); anchor.href = url; anchor.download = name; anchor.click();
      } finally { URL.revokeObjectURL(url); }
    } catch (failure) { setError(getApiErrorMessage(failure, "The delivery file could not be downloaded. Try again.")); }
    finally { setDownloading(null); }
  }
  return <article className="space-y-4 rounded-xl border bg-card/60 p-5">
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div><h3 className="font-semibold wrap-anywhere">{delivery.sequenceNumber}. {delivery.milestoneTitle} · Version {delivery.version}</h3>
        <p className="mt-1 text-sm text-muted-foreground">Submitted by {delivery.freelancerName} · {new Date(delivery.submittedAt).toLocaleString()}</p></div>
      <span className="rounded-full border px-3 py-1 text-xs">{delivery.isLatest ? "Latest version for this milestone" : "Earlier version"}</span>
    </header>
    <p className="whitespace-pre-wrap wrap-anywhere text-sm leading-6">{delivery.note}</p>
    <dl className="grid gap-4 text-sm sm:grid-cols-2">
      <div><dt className="text-xs text-muted-foreground">Submission ID</dt><dd className="mt-1 break-all">{delivery.id}</dd></div>
      <div><dt className="text-xs text-muted-foreground">Submitted by account</dt><dd className="mt-1 break-all">{delivery.freelancerId}</dd></div>
      <div><dt className="text-xs text-muted-foreground">Milestone ID</dt><dd className="mt-1 break-all">{delivery.milestoneId}</dd></div>
      <div><dt className="text-xs text-muted-foreground">Current milestone status</dt><dd className="mt-1">{delivery.currentMilestoneStatus.replace(/([a-z])([A-Z])/g, "$1 $2")}</dd></div>
    </dl>
    {delivery.clientFeedback ? <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-4">
      <h4 className="text-sm font-semibold">Client requested changes to this version</h4>
      <p className="mt-2 whitespace-pre-wrap wrap-anywhere text-sm">{delivery.clientFeedback}</p>
      {delivery.reviewedAt && <p className="mt-2 text-xs text-muted-foreground">Recorded {new Date(delivery.reviewedAt).toLocaleString()}</p>}
    </div> : <p className="text-sm text-muted-foreground">No change request is recorded for this version. This does not confirm approval or payment.</p>}
    {(preview || source) && <div className="flex flex-wrap gap-3">
      {preview && <a className="text-sm text-electric underline" href={preview} target="_blank" rel="noopener noreferrer">Open delivery preview</a>}
      {source && <a className="text-sm text-electric underline" href={source} target="_blank" rel="noopener noreferrer">Open source files</a>}
    </div>}
    {delivery.attachments.length ? <ul className="space-y-3" aria-label="Delivery files">{delivery.attachments.map((file) => <li key={file.id} className="rounded-lg border p-3">
      <Button variant="outline" size="sm" disabled={downloading !== null} onClick={() => void download(file.id, file.fileName)} className="h-auto max-w-full whitespace-normal wrap-anywhere">{downloading === file.id ? "Downloading…" : "Download"} {file.fileName}</Button>
      <p className="mt-2 break-all text-xs text-muted-foreground">{file.contentType} · {file.fileSize.toLocaleString()} bytes</p>
      <details className="mt-2 text-xs text-muted-foreground"><summary className="cursor-pointer">File checksum (SHA-256)</summary><p className="mt-2 break-all">{file.sha256}</p></details>
    </li>)}</ul> : <p className="text-sm text-muted-foreground">No files attached to this version.</p>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
  </article>;
}
