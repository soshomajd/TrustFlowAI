"use client";

import { FileUp, LoaderCircle, Send, Trash2, TriangleAlert } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useSubmitMilestone } from "@/features/milestones/hooks/use-submit-milestone";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

const maximumFileSize = 25 * 1024 * 1024;
const maximumTotalSize = 100 * 1024 * 1024;
const allowedTypes = new Set(["application/pdf", "image/jpeg", "image/png", "image/webp"]);

export function SubmitMilestoneDialog({ projectId, milestoneId }: { projectId: string; milestoneId: string }) {
  const [open, setOpen] = useState(false);
  const [note, setNote] = useState("");
  const [previewLink, setPreviewLink] = useState("");
  const [sourceFilesLink, setSourceFilesLink] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [localError, setLocalError] = useState<string | null>(null);
  const mutation = useSubmitMilestone(projectId, milestoneId);

  function reset() {
    setNote("");
    setPreviewLink("");
    setSourceFilesLink("");
    setFiles([]);
    setLocalError(null);
    mutation.reset();
  }

  function addFiles(nextFiles: File[]) {
    setLocalError(null);
    const combined = [...files, ...nextFiles];
    if (combined.length > 5) return setLocalError("You can attach up to 5 files.");
    if (nextFiles.some((file) => !allowedTypes.has(file.type)))
      return setLocalError("Only PDF, JPG, PNG and WebP files are allowed.");
    if (nextFiles.some((file) => file.size <= 0 || file.size > maximumFileSize))
      return setLocalError("Each file must be 25 MB or smaller.");
    if (combined.reduce((total, file) => total + file.size, 0) > maximumTotalSize)
      return setLocalError("All files together must be 100 MB or smaller.");
    setFiles(combined);
  }

  function submit() {
    setLocalError(null);
    if (!note.trim()) return setLocalError("Add a short delivery note.");
    if (files.length === 0 && !previewLink.trim() && !sourceFilesLink.trim())
      return setLocalError("Attach a file or add a delivery link.");

    mutation.mutate({ note: note.trim(), previewLink, sourceFilesLink, files }, {
      onSuccess: () => {
        setOpen(false);
        reset();
      },
    });
  }

  const error = localError ?? (mutation.isError
    ? getApiErrorMessage(mutation.error, "The work could not be submitted.")
    : null);

  return (
    <Dialog open={open} onOpenChange={(next) => {
      if (mutation.isPending) return;
      setOpen(next);
      if (next) reset();
    }}>
      <DialogTrigger asChild>
        <Button type="button"><Send className="size-4" />Submit work</Button>
      </DialogTrigger>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Submit work for review</DialogTitle>
          <DialogDescription>Add a clear note and at least one file or link. The client will review this version.</DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor={`delivery-note-${milestoneId}`}>Delivery note</Label>
            <Textarea id={`delivery-note-${milestoneId}`} value={note} onChange={(event) => setNote(event.target.value)} maxLength={2000} rows={4} placeholder="What did you finish? Add any simple instructions for the client." disabled={mutation.isPending} />
            <p className="text-xs text-muted-foreground">{note.length}/2000 characters</p>
          </div>

          <div className="space-y-2">
            <Label htmlFor={`delivery-files-${milestoneId}`}>Files</Label>
            <Input id={`delivery-files-${milestoneId}`} type="file" multiple accept=".pdf,.jpg,.jpeg,.png,.webp,application/pdf,image/jpeg,image/png,image/webp" disabled={mutation.isPending || files.length >= 5} onChange={(event) => {
              addFiles(Array.from(event.target.files ?? []));
              event.target.value = "";
            }} />
            <p className="text-xs text-muted-foreground">Up to 5 files. PDF, JPG, PNG or WebP. 25 MB per file, 100 MB total.</p>
            {files.length > 0 && <ul className="space-y-2">{files.map((file, index) => (
              <li key={`${file.name}-${file.lastModified}-${index}`} className="flex items-center justify-between gap-3 rounded-lg border bg-background/40 px-3 py-2 text-sm">
                <span className="min-w-0 truncate"><FileUp className="mr-2 inline size-4 text-electric" />{file.name} · {formatBytes(file.size)}</span>
                <Button type="button" variant="ghost" size="icon-sm" aria-label={`Remove ${file.name}`} onClick={() => setFiles((current) => current.filter((_, itemIndex) => itemIndex !== index))}><Trash2 className="size-3.5" /></Button>
              </li>
            ))}</ul>}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2"><Label htmlFor={`preview-link-${milestoneId}`}>Preview link (optional)</Label><Input id={`preview-link-${milestoneId}`} type="url" value={previewLink} onChange={(event) => setPreviewLink(event.target.value)} maxLength={1000} placeholder="https://figma.com/..." disabled={mutation.isPending} /></div>
            <div className="space-y-2"><Label htmlFor={`source-link-${milestoneId}`}>Source files link (optional)</Label><Input id={`source-link-${milestoneId}`} type="url" value={sourceFilesLink} onChange={(event) => setSourceFilesLink(event.target.value)} maxLength={1000} placeholder="https://drive.google.com/..." disabled={mutation.isPending} /></div>
          </div>

          {error && <div role="alert" className="flex gap-3 rounded-xl border border-destructive/30 bg-destructive/10 p-4"><TriangleAlert className="mt-0.5 size-5 shrink-0 text-destructive" /><p className="text-sm text-destructive">{error}</p></div>}
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" disabled={mutation.isPending} onClick={() => setOpen(false)}>Cancel</Button>
          <Button type="button" disabled={mutation.isPending} onClick={submit}>{mutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Send className="size-4" />}{mutation.isPending ? "Scanning and submitting" : "Submit work"}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function formatBytes(value: number) {
  if (value < 1024 * 1024) return `${Math.max(1, Math.round(value / 1024))} KB`;
  return `${(value / (1024 * 1024)).toFixed(1)} MB`;
}
