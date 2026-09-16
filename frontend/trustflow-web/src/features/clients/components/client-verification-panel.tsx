"use client";

import { CheckCircle2, FileCheck2, LoaderCircle, RotateCcw, Send, ShieldCheck, TriangleAlert } from "lucide-react";
import { useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useResetClientVerification, useSubmitClientVerification } from "@/features/clients/hooks/use-client-profile";
import type { ClientProfile } from "@/features/clients/types/client-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

const MAX_BYTES = 5 * 1024 * 1024;

export function ClientVerificationPanel({ profile }: { profile: ClientProfile }) {
  const submit = useSubmitClientVerification();
  const reset = useResetClientVerification();
  const inputRef = useRef<HTMLInputElement>(null);
  const [legalName, setLegalName] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [registrationNumber, setRegistrationNumber] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [localError, setLocalError] = useState("");
  const busy = submit.isPending || reset.isPending;

  function send(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setLocalError("");
    if (!legalName.trim()) return setLocalError("Enter the legal name shown on the document.");
    if (!/^[A-Za-z]{2}$/.test(countryCode.trim())) return setLocalError("Use a two-letter country code, for example DE or IR.");
    if (profile.accountType === "Company" && !registrationNumber.trim()) return setLocalError("Enter the company registration number.");
    if (!file) return setLocalError("Choose a PDF document.");
    if (file.type !== "application/pdf" || !file.name.toLowerCase().endsWith(".pdf")) return setLocalError("Only PDF documents are accepted.");
    if (file.size <= 0 || file.size > MAX_BYTES) return setLocalError("The PDF must be 5 MB or smaller.");
    submit.mutate({ legalName: legalName.trim(), countryCode: countryCode.trim().toUpperCase(), registrationNumber: registrationNumber.trim(), file }, {
      onSuccess: () => { setLegalName(""); setCountryCode(""); setRegistrationNumber(""); setFile(null); if (inputRef.current) inputRef.current.value = ""; },
    });
  }

  function startOver() {
    if (!window.confirm("Remove the current verification status and start again?")) return;
    reset.mutate();
  }

  return <section className="rounded-2xl border bg-card/70 p-5 sm:p-6">
    <div className="flex items-start gap-3">
      <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10"><ShieldCheck className="size-5 text-electric" /></div>
      <div><h2 className="font-semibold">Identity verification</h2><p className="mt-1 text-sm leading-6 text-muted-foreground">Build trust with one document. This section is private and never appears on your public profile.</p></div>
    </div>

    {profile.verificationStatus === "Pending" && <StatusBox tone="amber" icon={FileCheck2} title="Review in progress" text={`Submitted ${formatDate(profile.verificationSubmittedAt)}. An admin can review the document; other users cannot access it.`} />}
    {profile.verificationStatus === "Verified" && <StatusBox tone="green" icon={CheckCircle2} title="Identity document reviewed" text={`Approved${profile.identityVerifiedAt ? ` on ${formatDate(profile.identityVerifiedAt)}` : ""}. Your public profile now shows the reviewed badge.`} />}
    {profile.verificationStatus === "Rejected" && <StatusBox tone="red" icon={TriangleAlert} title="More information is needed" text={profile.verificationRejectionReason || "The document could not be approved. Check the file and submit again."} />}

    {(profile.verificationStatus === "NotSubmitted" || profile.verificationStatus === "Rejected") && <form onSubmit={send} className="mt-6 space-y-4" noValidate>
      <fieldset disabled={busy} className="space-y-4">
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id="legal-name" label={profile.accountType === "Company" ? "Registered company name" : "Legal name"} hint="Enter it exactly as shown on the document."><Input id="legal-name" value={legalName} onChange={(event) => setLegalName(event.target.value)} maxLength={150} autoComplete="name" /></Field>
          <Field id="country-code" label="Issuing country" hint="Two-letter code, for example IR or DE."><Input id="country-code" value={countryCode} onChange={(event) => setCountryCode(event.target.value.toUpperCase())} maxLength={2} placeholder="IR" className="uppercase" /></Field>
        </div>
        {profile.accountType === "Company" && <Field id="registration-number" label="Company registration number"><Input id="registration-number" value={registrationNumber} onChange={(event) => setRegistrationNumber(event.target.value)} maxLength={100} /></Field>}
        <Field id="identity-file" label={profile.accountType === "Company" ? "Business registration document" : "Identity document"} hint="PDF only · up to 5 MB · up to 25 pages. Active content and malware are rejected.">
          <Input ref={inputRef} id="identity-file" type="file" accept="application/pdf,.pdf" onChange={(event) => { setLocalError(""); setFile(event.target.files?.[0] ?? null); }} />
        </Field>
      </fieldset>
      {(localError || submit.isError) && <p role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">{localError || getApiErrorMessage(submit.error, "The document could not be submitted.")}</p>}
      <div className="rounded-xl border border-primary/15 bg-primary/5 p-4 text-xs leading-5 text-muted-foreground">The document and legal details are encrypted at rest, malware-scanned, available only to admins during review, and removed after a decision. TrustFlow keeps the result and audit timestamps.</div>
      <Button type="submit" disabled={busy}>{submit.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Send className="size-4" />}{submit.isPending ? "Submitting..." : "Submit for review"}</Button>
    </form>}

    {(profile.verificationStatus === "Pending" || profile.verificationStatus === "Verified") && <div className="mt-5 border-t pt-5"><Button type="button" variant="ghost" size="sm" disabled={busy} onClick={startOver}>{reset.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <RotateCcw className="size-4" />}Reset verification</Button>{reset.isError && <p role="alert" className="mt-3 text-sm text-destructive">{getApiErrorMessage(reset.error)}</p>}</div>}
  </section>;
}

function Field({ id, label, hint, children }: { id: string; label: string; hint?: string; children: React.ReactNode }) { return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{children}{hint && <p className="text-xs leading-5 text-muted-foreground">{hint}</p>}</div>; }

function StatusBox({ tone, icon: Icon, title, text }: { tone: "amber" | "green" | "red"; icon: typeof CheckCircle2; title: string; text: string }) {
  const styles = { amber: "border-amber-500/25 bg-amber-500/5 text-amber-300", green: "border-emerald-500/25 bg-emerald-500/5 text-emerald-300", red: "border-red-500/25 bg-red-500/5 text-red-300" };
  return <div className={`mt-5 flex gap-3 rounded-xl border p-4 ${styles[tone]}`}><Icon className="mt-0.5 size-5 shrink-0" /><div><p className="text-sm font-medium">{title}</p><p className="mt-1 text-xs leading-5 text-muted-foreground">{text}</p></div></div>;
}

function formatDate(value: string | null) { if (!value) return "recently"; return new Intl.DateTimeFormat("en-US", { dateStyle: "medium" }).format(new Date(value)); }
