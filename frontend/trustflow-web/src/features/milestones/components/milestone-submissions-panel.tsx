"use client";

import { Download, ExternalLink, Eye, FileCheck2, LoaderCircle, LockKeyhole, RefreshCw, TriangleAlert } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { downloadMilestoneSubmissionFile } from "@/features/milestones/api/milestones-api";
import { DeliveryPreviewDialog } from "@/features/milestones/components/delivery-preview-dialog";
import { useMilestoneSubmissions } from "@/features/milestones/hooks/use-milestone-submissions";
import type { MilestoneSubmission, MilestoneSubmissionAttachment } from "@/features/milestones/types/milestone";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

type ViewerRole = "Client" | "Freelancer";

export function MilestoneSubmissionsPanel({
  projectId,
  milestoneId,
  viewerRole,
}: {
  projectId: string;
  milestoneId: string;
  viewerRole: ViewerRole;
}) {
  const query = useMilestoneSubmissions(projectId, milestoneId);
  if (query.isLoading) return <div className="mt-5 h-24 animate-pulse rounded-xl bg-muted/30" />;
  if (query.isError) return <div className="mt-5 flex items-center justify-between gap-3 rounded-xl border border-destructive/30 bg-destructive/5 p-4"><p className="text-sm text-destructive">{getApiErrorMessage(query.error, "Delivery could not be loaded.")}</p><Button type="button" size="sm" variant="outline" onClick={() => void query.refetch()}><RefreshCw className="size-3.5" />Retry</Button></div>;
  const submissions = query.data ?? [];
  if (submissions.length === 0) return null;

  return (
    <div className="mt-5 border-t pt-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2"><FileCheck2 className="size-4 text-electric" /><h4 className="text-sm font-semibold">Delivered work</h4></div>
        <Button type="button" size="sm" variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}><RefreshCw className={`size-3.5 ${query.isFetching ? "animate-spin" : ""}`} />Refresh deliveries</Button>
      </div>
      <SubmissionCard projectId={projectId} milestoneId={milestoneId} viewerRole={viewerRole} submission={submissions[0]} />
      {submissions.length > 1 && <details className="mt-3 rounded-xl border bg-background/20 p-4"><summary className="cursor-pointer text-sm font-medium">Previous versions ({submissions.length - 1})</summary><div className="mt-4 space-y-3">{submissions.slice(1).map((submission) => <SubmissionCard key={submission.id} projectId={projectId} milestoneId={milestoneId} viewerRole={viewerRole} submission={submission} compact />)}</div></details>}
    </div>
  );
}

export function SubmissionCard({
  projectId,
  milestoneId,
  viewerRole,
  submission,
  compact = false,
}: {
  projectId: string;
  milestoneId: string;
  viewerRole: ViewerRole;
  submission: MilestoneSubmission;
  compact?: boolean;
}) {
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [preview, setPreview] = useState<MilestoneSubmissionAttachment | null>(null);
  const [error, setError] = useState<string | null>(null);
  const canDownloadOriginals = viewerRole === "Freelancer" || submission.originalsUnlocked === true;
  const sourceFilesLink = canDownloadOriginals ? submission.sourceFilesLink : null;

  async function download(attachmentId: string, fileName: string) {
    setDownloadingId(attachmentId);
    setError(null);
    try {
      const blob = await downloadMilestoneSubmissionFile(projectId, milestoneId, submission.id, attachmentId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      try {
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
      } finally {
        URL.revokeObjectURL(url);
      }
    } catch (downloadError) {
      setError(getApiErrorMessage(downloadError, "The original file is unavailable. Refresh deliveries to check payment status."));
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <article className={`${compact ? "mt-0" : "mt-3"} rounded-xl border bg-background/35 p-4`}>
      <div className="flex flex-wrap items-center justify-between gap-2"><span className="rounded-full border border-primary/25 bg-primary/10 px-2.5 py-1 text-xs text-electric">Version {submission.version}</span><time className="text-xs text-muted-foreground">{new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" }).format(new Date(submission.submittedAt))}</time></div>
      <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{submission.note}</p>

      {!canDownloadOriginals && (submission.attachments.length > 0 || submission.hasSourceFiles) && (
        <div className="mt-3 flex gap-2 rounded-lg border bg-muted/20 p-3 text-sm text-muted-foreground">
          <LockKeyhole className="mt-0.5 size-4 shrink-0" />
          <p>Original files and source links unlock after this milestone is paid in full, before the platform fee. A partial award keeps them locked. Review the previews before you decide.</p>
        </div>
      )}

      {(submission.previewLink || sourceFilesLink) && (
        <div className="mt-3 flex flex-wrap gap-2">
          {submission.previewLink && <Button asChild variant="outline" size="sm"><a href={submission.previewLink} target="_blank" rel="noopener noreferrer"><ExternalLink className="size-3.5" />Open review link</a></Button>}
          {sourceFilesLink && <Button asChild variant="outline" size="sm"><a href={sourceFilesLink} target="_blank" rel="noopener noreferrer"><ExternalLink className="size-3.5" />Open source files</a></Button>}
        </div>
      )}

      {submission.attachments.length > 0 && (
        <ul className="mt-3 space-y-2">
          {submission.attachments.map((attachment) => (
            <li key={attachment.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-background/20 p-3">
              <span className="min-w-0 break-all text-xs text-muted-foreground">{attachment.fileName} · {formatBytes(attachment.fileSize)}</span>
              {canDownloadOriginals ? (
                <Button type="button" variant="outline" size="sm" disabled={downloadingId !== null} onClick={() => void download(attachment.id, attachment.fileName)}>{downloadingId === attachment.id ? <LoaderCircle className="size-3.5 animate-spin" /> : <Download className="size-3.5" />}Download original</Button>
              ) : attachment.hasPreview ? (
                <Button type="button" variant="outline" size="sm" onClick={() => setPreview(attachment)}><Eye className="size-3.5" />Review preview</Button>
              ) : (
                <span className="text-xs text-muted-foreground">Preview unavailable. Ask for a review sample.</span>
              )}
            </li>
          ))}
        </ul>
      )}

      {submission.clientFeedback && <div className="mt-3 rounded-lg border border-amber-500/20 bg-amber-500/5 p-3"><p className="text-xs font-semibold text-amber-300">Client requested changes</p><p className="mt-1 text-sm text-muted-foreground">{submission.clientFeedback}</p></div>}
      {error && <p role="alert" className="mt-3 flex gap-2 text-sm text-destructive"><TriangleAlert className="mt-0.5 size-4 shrink-0" />{error}</p>}
      {preview && <DeliveryPreviewDialog projectId={projectId} milestoneId={milestoneId} submissionId={submission.id} attachment={preview} onClose={() => setPreview(null)} />}
    </article>
  );
}

function formatBytes(value: number) {
  if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}
