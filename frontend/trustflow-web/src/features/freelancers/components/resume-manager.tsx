"use client";

import { useRef, useState } from "react";
import { FileText, LoaderCircle, Trash2, Upload } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ResumeDownloadButton } from "@/features/freelancers/components/resume-download-button";
import { useDeleteFreelancerResume, useUploadFreelancerResume } from "@/features/freelancers/hooks/use-freelancer-profile";
import type { FreelancerProfile } from "@/features/freelancers/types/freelancer-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

const maximumFileSize = 5 * 1024 * 1024;

export function ResumeManager({ profile }: { profile: FreelancerProfile }) {
  const inputRef = useRef<HTMLInputElement>(null);
  const upload = useUploadFreelancerResume();
  const remove = useDeleteFreelancerResume();
  const [clientError, setClientError] = useState<string | null>(null);
  const busy = upload.isPending || remove.isPending;

  function chooseFile(file: File | undefined) {
    setClientError(null);
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".pdf") || file.type !== "application/pdf") {
      setClientError("Choose a PDF file.");
      return;
    }
    if (file.size === 0 || file.size > maximumFileSize) {
      setClientError("The PDF must be no larger than 5 MB.");
      return;
    }
    upload.mutate(file, { onSettled: () => { if (inputRef.current) inputRef.current.value = ""; } });
  }

  function removeResume() {
    if (window.confirm("Remove your current resume? You can upload another one later.")) remove.mutate();
  }

  const error = clientError ?? (upload.isError ? getApiErrorMessage(upload.error, "The resume could not be uploaded.") : remove.isError ? getApiErrorMessage(remove.error, "The resume could not be removed.") : null);

  return (
    <section className="rounded-2xl border bg-card/70 p-5">
      <div className="flex items-center gap-2"><FileText className="size-4 text-electric" /><h2 className="font-semibold">Resume</h2></div>
      <p className="mt-2 text-sm leading-6 text-muted-foreground">Optional PDF, up to 5 MB and 25 pages. Files are scanned and downloaded as attachments.</p>
      {profile.hasResume && <div className="mt-4 rounded-xl border bg-background/40 p-3 text-xs text-muted-foreground"><p className="font-medium text-foreground">resume.pdf</p><p className="mt-1">{formatBytes(profile.resumeFileSize)}{profile.resumeUploadedAt ? ` · Updated ${formatDate(profile.resumeUploadedAt)}` : ""}</p></div>}
      <input ref={inputRef} id="resume-file" type="file" accept="application/pdf,.pdf" className="sr-only" disabled={busy} onChange={(event) => chooseFile(event.target.files?.[0])} />
      <div className="mt-4 space-y-2">
        <Button type="button" className="w-full" disabled={busy} onClick={() => inputRef.current?.click()}>{upload.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Upload className="size-4" />}{upload.isPending ? "Checking and uploading…" : profile.hasResume ? "Replace resume" : "Upload resume"}</Button>
        {profile.hasResume && <ResumeDownloadButton userId={profile.userId} />}
        {profile.hasResume && <Button type="button" variant="ghost" className="w-full text-destructive" disabled={busy} onClick={removeResume}>{remove.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Trash2 className="size-4" />}{remove.isPending ? "Removing…" : "Remove resume"}</Button>}
      </div>
      {error && <p role="alert" className="mt-3 text-xs leading-5 text-destructive">{error}</p>}
    </section>
  );
}

function formatBytes(value: number | null) {
  return value === null ? "PDF" : `${(value / 1024).toFixed(value < 1024 * 100 ? 1 : 0)} KB`;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("en-US", { dateStyle: "medium" }).format(new Date(value));
}
