"use client";

import { useEffect, useRef } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { enterLocalAdminPreview } from "@/features/admin/api/admin-auth-api";
import { getAdminReturnPath } from "@/features/auth/lib/sign-in-path";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";
import { Button } from "@/components/ui/button";

export function LocalAdminPreview({ next }: { next?: string }) {
  const router = useRouter();
  const cache = useQueryClient();
  const status = useAuthStore((state) => state.status);
  const setSession = useAuthStore((state) => state.setSession);
  const started = useRef(false);
  const { mutate, isError, error } = useMutation({
    mutationFn: enterLocalAdminPreview,
    onSuccess: (session) => {
      cache.clear();
      setSession(session);
      router.replace(getAdminReturnPath(next));
    },
  });
  useEffect(() => {
    if (status === "idle" || status === "checking" || started.current) return;
    started.current = true;
    mutate();
  }, [mutate, status]);

  return <section className="w-full max-w-md rounded-2xl border bg-card p-7">
    <h1 className="text-xl font-semibold">Local administrator preview</h1>
    {isError ? <div role="alert" className="mt-4 space-y-4">
      <p>{getApiErrorMessage(error, "Local preview is unavailable.")}</p>
      <Button onClick={() => mutate()}>Try again</Button>
    </div> : <p role="status" className="mt-4 text-sm text-muted-foreground">Opening the administrator panel…</p>}
  </section>;
}
