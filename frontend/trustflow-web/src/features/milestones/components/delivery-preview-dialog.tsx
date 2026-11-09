"use client";

import { LoaderCircle, RefreshCw, TriangleAlert } from "lucide-react";
import Image from "next/image";
import { useEffect, useState } from "react";

import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { getMilestoneSubmissionPreview } from "@/features/milestones/api/milestones-api";
import type { MilestoneSubmissionAttachment } from "@/features/milestones/types/milestone";

export function DeliveryPreviewDialog({
  projectId,
  milestoneId,
  submissionId,
  attachment,
  onClose,
}: {
  projectId: string;
  milestoneId: string;
  submissionId: string;
  attachment: MilestoneSubmissionAttachment;
  onClose: () => void;
}) {
  const [attempt, setAttempt] = useState(0);

  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
        <DialogHeader>
          <DialogTitle>Review preview</DialogTitle>
          <DialogDescription className="break-all">{attachment.fileName}. This is a limited review sample, not the original file. Previews and review links can still be copied.</DialogDescription>
        </DialogHeader>
        <PreviewImage key={`${attachment.id}-${attempt}`} projectId={projectId} milestoneId={milestoneId} submissionId={submissionId} attachmentId={attachment.id} onRetry={() => setAttempt((value) => value + 1)} />
      </DialogContent>
    </Dialog>
  );
}

function PreviewImage({
  projectId,
  milestoneId,
  submissionId,
  attachmentId,
  onRetry,
}: {
  projectId: string;
  milestoneId: string;
  submissionId: string;
  attachmentId: string;
  onRetry: () => void;
}) {
  const [url, setUrl] = useState<string | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    let objectUrl: string | null = null;
    let active = true;

    void getMilestoneSubmissionPreview(projectId, milestoneId, submissionId, attachmentId, controller.signal)
      .then((blob) => {
        if (!active) return;
        if (blob.type !== "image/png") throw new Error("Unsupported preview response");
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch(() => { if (active) setFailed(true); });

    return () => {
      active = false;
      controller.abort();
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [projectId, milestoneId, submissionId, attachmentId]);

  if (failed) return (
    <div role="alert" className="space-y-3 rounded-xl border border-destructive/30 bg-destructive/5 p-4">
      <p className="flex gap-2 text-sm text-destructive"><TriangleAlert className="size-4 shrink-0" />The preview could not be displayed. Retry or ask the freelancer for a review sample.</p>
      <Button type="button" variant="outline" size="sm" onClick={onRetry}><RefreshCw className="size-3.5" />Retry preview</Button>
    </div>
  );
  if (!url) return <div role="status" className="flex min-h-40 items-center justify-center gap-2 text-sm text-muted-foreground"><LoaderCircle className="size-4 animate-spin" />Loading preview</div>;

  return <Image src={url} alt="Limited, watermarked delivery preview" width={1200} height={900} unoptimized className="mx-auto h-auto max-h-[65vh] w-auto max-w-full rounded-lg object-contain" onError={() => setFailed(true)} />;
}
