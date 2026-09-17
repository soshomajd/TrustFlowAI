"use client";

import { Download, ExternalLink, FileCheck2, LoaderCircle, RefreshCw, TriangleAlert } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { downloadMilestoneSubmissionFile } from "@/features/milestones/api/milestones-api";
import { useMilestoneSubmissions } from "@/features/milestones/hooks/use-milestone-submissions";
import type { MilestoneSubmission } from "@/features/milestones/types/milestone";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function MilestoneSubmissionsPanel({ projectId, milestoneId }: { projectId: string; milestoneId: string }) {
  const query = useMilestoneSubmissions(projectId, milestoneId);
  if (query.isLoading) return <div className="mt-5 h-24 animate-pulse rounded-xl bg-muted/30" />;
  if (query.isError) return <div className="mt-5 flex items-center justify-between gap-3 rounded-xl border border-destructive/30 bg-destructive/5 p-4"><p className="text-sm text-destructive">{getApiErrorMessage(query.error, "Delivery could not be loaded.")}</p><Button type="button" size="sm" variant="outline" onClick={() => void query.refetch()}><RefreshCw className="size-3.5" />Retry</Button></div>;
  const submissions = query.data ?? [];
  if (submissions.length === 0) return null;

  return (
    <div className="mt-5 border-t pt-5">
      <div className="flex items-center gap-2"><FileCheck2 className="size-4 text-electric" /><h4 className="text-sm font-semibold">Delivered work</h4></div>
      <SubmissionCard projectId={projectId} milestoneId={milestoneId} submission={submissions[0]} />
      {submissions.length > 1 && <details className="mt-3 rounded-xl border bg-background/20 p-4"><summary className="cursor-pointer text-sm font-medium">Previous versions ({submissions.length - 1})</summary><div className="mt-4 space-y-3">{submissions.slice(1).map((submission) => <SubmissionCard key={submission.id} projectId={projectId} milestoneId={milestoneId} submission={submission} compact />)}</div></details>}
    </div>
  );
}

function SubmissionCard({ projectId, milestoneId, submission, compact = false }: { projectId: string; milestoneId: string; submission: MilestoneSubmission; compact?: boolean }) {
  const [downloadingId, setDownloadingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function download(attachmentId: string, fileName: string) {
    setDownloadingId(attachmentId);
    setError(null);
    try {
      const blob = await downloadMilestoneSubmissionFile(projectId, milestoneId, submission.id, attachmentId);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = fileName;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (downloadError) {
      setError(getApiErrorMessage(downloadError, "The file could not be downloaded."));
    } finally {
      setDownloadingId(null);
    }
  }

  return (
    <article className={`${compact ? "mt-0" : "mt-3"} rounded-xl border bg-background/35 p-4`}>
      <div className="flex flex-wrap items-center justify-between gap-2"><span className="rounded-full border border-primary/25 bg-primary/10 px-2.5 py-1 text-xs text-electric">Version {submission.version}</span><time className="text-xs text-muted-foreground">{new Intl.DateTimeFormat("en-US", { dateStyle: "medium", timeStyle: "short" }).format(new Date(submission.submittedAt))}</time></div>
      <p className="mt-3 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{submission.note}</p>
      {(submission.previewLink || submission.sourceFilesLink) && <div className="mt-3 flex flex-wrap gap-2">{submission.previewLink && <Button asChild variant="outline" size="sm"><a href={submission.previewLink} target="_blank" rel="noreferrer"><ExternalLink className="size-3.5" />Open preview</a></Button>}{submission.sourceFilesLink && <Button asChild variant="outline" size="sm"><a href={submission.sourceFilesLink} target="_blank" rel="noreferrer"><ExternalLink className="size-3.5" />Open source files</a></Button>}</div>}
      {submission.attachments.length > 0 && <div className="mt-3 flex flex-wrap gap-2">{submission.attachments.map((attachment) => <Button key={attachment.id} type="button" variant="outline" size="sm" disabled={downloadingId !== null} onClick={() => void download(attachment.id, attachment.fileName)}>{downloadingId === attachment.id ? <LoaderCircle className="size-3.5 animate-spin" /> : <Download className="size-3.5" />}{attachment.fileName} · {formatBytes(attachment.fileSize)}</Button>)}</div>}
      {submission.clientFeedback && <div className="mt-3 rounded-lg border border-amber-500/20 bg-amber-500/5 p-3"><p className="text-xs font-semibold text-amber-300">Client requested changes</p><p className="mt-1 text-sm text-muted-foreground">{submission.clientFeedback}</p></div>}
      {error && <p role="alert" className="mt-3 flex gap-2 text-sm text-destructive"><TriangleAlert className="mt-0.5 size-4 shrink-0" />{error}</p>}
    </article>
  );
}

function formatBytes(value: number) {
  if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}
