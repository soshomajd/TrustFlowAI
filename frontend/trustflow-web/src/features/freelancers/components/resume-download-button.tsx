"use client";

import { useState } from "react";
import { Download, LoaderCircle } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { downloadFreelancerResume } from "@/features/freelancers/api/freelancers-api";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function ResumeDownloadButton({ userId }: { userId: string }) {
  const [downloading, setDownloading] = useState(false);

  async function download() {
    setDownloading(true);
    try {
      await downloadFreelancerResume(userId);
    } catch (error) {
      toast.error(getApiErrorMessage(error, "The resume could not be downloaded."));
    } finally {
      setDownloading(false);
    }
  }

  return <Button type="button" variant="outline" className="mt-4 w-full" disabled={downloading} onClick={() => void download()}>{downloading ? <LoaderCircle className="size-4 animate-spin" /> : <Download className="size-4" />}{downloading ? "Downloading…" : "Download resume (PDF)"}</Button>;
}
