"use client";

import { useState, type FormEvent } from "react";
import { ShieldCheck } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { AdminMfaPrompt } from "@/features/admin/api/admin-auth-api";
import { useAdminMfa } from "@/features/admin/hooks/use-admin-auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function AdminMfaForm({ prompt, next, onRestart }: { prompt: AdminMfaPrompt; next?: string; onRestart: () => void }) {
  const mutation = useAdminMfa(next);
  const [code, setCode] = useState("");
  const submit = (event: FormEvent) => {
    event.preventDefault();
    if (/^[0-9]{6}$/.test(code)) mutation.mutate({ challengeToken: prompt.challengeToken, code });
  };
  return <section className="w-full max-w-md rounded-2xl border border-primary/30 bg-card/95 p-7 shadow-blue-glow">
    <ShieldCheck className="size-10 text-electric" /><h1 className="mt-5 text-2xl font-semibold">Verify administrator sign-in</h1>
    <p className="mt-3 text-sm text-muted-foreground">Enter the 6-digit code sent to {prompt.maskedEmail}. This sign-in request expires in 5 minutes. A code can be used only once.</p>
    <form onSubmit={submit} className="mt-6 space-y-4">
      <Label htmlFor="admin-signin-code">Email sign-in code</Label>
      <Input id="admin-signin-code" inputMode="numeric" autoComplete="one-time-code" maxLength={6} value={code} onChange={(event) => setCode(event.target.value.replace(/\D/g, ""))} disabled={mutation.isPending} />
      {mutation.isError && <p role="alert" className="text-sm text-destructive">{getApiErrorMessage(mutation.error, "The code could not be verified.")}</p>}
      <Button type="submit" disabled={mutation.isPending || !/^[0-9]{6}$/.test(code)} className="w-full">{mutation.isPending ? "Verifying…" : "Verify and open administration"}</Button>
      <Button type="button" variant="ghost" onClick={onRestart} disabled={mutation.isPending}>Start sign-in again</Button>
      <p className="text-xs text-muted-foreground">To request another code, restart sign-in after 60 seconds. Never share a sign-in code.</p>
    </form>
  </section>;
}
