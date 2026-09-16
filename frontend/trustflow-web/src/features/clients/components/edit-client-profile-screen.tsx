"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { Building2, Check, Eye, LoaderCircle, RefreshCw, Save, UserRound } from "lucide-react";
import Link from "next/link";
import { useEffect, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ClientVerificationPanel } from "@/features/clients/components/client-verification-panel";
import { useMyClientProfile, useUpdateClientProfile } from "@/features/clients/hooks/use-client-profile";
import { clientProfileSchema, type ClientProfileFormValues } from "@/features/clients/schemas/client-profile-schema";
import { companySizeLabels, type ClientAccountType, type ClientProfile, type CompanySize } from "@/features/clients/types/client-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function EditClientProfileScreen() {
  const query = useMyClientProfile();
  return <main className="mx-auto max-w-6xl px-4 py-8 sm:px-6">
    {query.isPending ? <Loading /> : query.isError ? <Notice message={getApiErrorMessage(query.error)} onRetry={() => void query.refetch()} /> : <Editor key={`${query.data.userId}-${query.data.updatedAt}`} profile={query.data} />}
  </main>;
}

function Editor({ profile }: { profile: ClientProfile }) {
  const mutation = useUpdateClientProfile();
  const form = useForm<ClientProfileFormValues>({ resolver: zodResolver(clientProfileSchema), defaultValues: toValues(profile) });
  const [accountType, companyName, overview, industry, location] = useWatch({ control: form.control, name: ["accountType", "companyName", "overview", "industry", "location"] });
  const statusLocked = ["Pending", "Verified"].includes(profile.verificationStatus);
  const essentials = [
    { label: accountType === "Company" ? "Company name" : "Account type", done: accountType === "Individual" || Boolean(companyName.trim()) },
    { label: "Short introduction", done: Boolean(overview.trim()) },
    { label: "Industry", done: Boolean(industry.trim()) },
    { label: "Location", done: Boolean(location.trim()) },
  ];
  const completed = essentials.filter((item) => item.done).length;
  const { errors, isDirty } = form.formState;

  useEffect(() => { if (!isDirty) return; const warn = (event: BeforeUnloadEvent) => event.preventDefault(); window.addEventListener("beforeunload", warn); return () => window.removeEventListener("beforeunload", warn); }, [isDirty]);

  function save(values: ClientProfileFormValues) { mutation.mutate(values, { onSuccess: (saved) => form.reset(toValues(saved)) }); }

  return <>
    <header className="mb-8 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
      <div><p className="text-xs font-medium uppercase tracking-[0.2em] text-electric">Your hiring identity</p><h1 className="mt-3 text-3xl font-semibold">My client profile</h1><p className="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">Give freelancers the context they need to trust the opportunity. The essentials take only a few minutes.</p></div>
      <Button asChild variant="outline" className="self-start"><Link href={`/dashboard/clients/${profile.userId}`}><Eye className="size-4" />View public profile</Link></Button>
    </header>

    <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_290px]">
      <div className="min-w-0 space-y-6">
        <form onSubmit={form.handleSubmit(save)} className="space-y-6" noValidate>
          <fieldset disabled={mutation.isPending} className="space-y-6">
            <Section title="Who is hiring?" description="Choose the account type and add the name people should recognize.">
              <div className="grid gap-3 sm:grid-cols-2">
                {(["Individual", "Company"] as ClientAccountType[]).map((value) => { const Icon = value === "Company" ? Building2 : UserRound; return <label key={value} className={`flex cursor-pointer gap-3 rounded-xl border p-4 transition ${accountType === value ? "border-primary/50 bg-primary/10" : "hover:border-primary/25"} ${statusLocked ? "cursor-not-allowed opacity-70" : ""}`}><input type="radio" value={value} {...form.register("accountType")} disabled={statusLocked || mutation.isPending} className="sr-only" /><Icon className="size-5 text-electric" /><span><span className="block text-sm font-medium">{value}</span><span className="mt-1 block text-xs leading-5 text-muted-foreground">{value === "Individual" ? "Hiring under your own name" : "Hiring for a registered business"}</span></span></label>; })}
              </div>
              {statusLocked && <p className="text-xs text-muted-foreground">Reset identity verification before changing the account type.</p>}
              {accountType === "Company" && <Field id="company-name" label="Company name" error={errors.companyName?.message}><Input id="company-name" {...form.register("companyName")} maxLength={120} placeholder="Acme Studio" /></Field>}
            </Section>

            <Section title="A useful introduction" description="Keep it short: what you do, what you usually hire for, and how you work.">
              <Field id="overview" label="About you or your company" hint={`${overview.length}/1000 characters`} error={errors.overview?.message}><Textarea id="overview" {...form.register("overview")} rows={6} maxLength={1000} placeholder="We are a small product team building tools for... We value clear communication and practical delivery." /></Field>
              <div className="grid gap-4 sm:grid-cols-2">
                <Field id="industry" label="Industry" error={errors.industry?.message}><Input id="industry" {...form.register("industry")} maxLength={80} placeholder="e.g. Education technology" /></Field>
                <Field id="location" label="Location" hint="City or country is enough." error={errors.location?.message}><Input id="location" {...form.register("location")} maxLength={100} placeholder="e.g. Tehran, Iran" /></Field>
              </div>
              <Field id="website" label="Website (optional)" error={errors.website?.message}><Input id="website" {...form.register("website")} type="url" maxLength={500} placeholder="https://..." /></Field>
              {accountType === "Company" && <Field id="company-size" label="Company size" error={errors.companySize?.message}><select id="company-size" {...form.register("companySize")} className="h-10 w-full rounded-lg border bg-background px-3 text-sm">{(Object.keys(companySizeLabels) as CompanySize[]).map((value) => <option key={value} value={value}>{companySizeLabels[value]}</option>)}</select></Field>}
            </Section>
          </fieldset>
          {mutation.isError && <p role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{getApiErrorMessage(mutation.error, "Your profile could not be saved. Your edits are still here.")}</p>}
          <div className="sticky bottom-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-background/95 p-4 shadow-lg backdrop-blur"><p role="status" className="text-xs text-muted-foreground">{mutation.isPending ? "Saving..." : isDirty ? "You have unsaved changes" : "All changes saved"}</p><Button type="submit" disabled={mutation.isPending || !isDirty}>{mutation.isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Save className="size-4" />}{mutation.isPending ? "Saving..." : "Save profile"}</Button></div>
        </form>
        <ClientVerificationPanel profile={profile} />
      </div>

      <aside className="space-y-5 lg:sticky lg:top-24">
        <section className="rounded-2xl border bg-card/70 p-5"><h2 className="font-semibold">A trustworthy introduction</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">These four details answer most freelancer questions before they apply.</p><div className="mt-5 h-1.5 overflow-hidden rounded-full bg-muted" role="progressbar" aria-valuenow={completed} aria-valuemin={0} aria-valuemax={4}><div className="h-full rounded-full bg-primary transition-all" style={{ width: `${completed / 4 * 100}%` }} /></div><p className="mt-2 text-xs text-muted-foreground">{completed} of 4 essentials added</p><ul className="mt-5 space-y-3">{essentials.map((item) => <li key={item.label} className="flex items-center gap-2 text-sm"><span className={`flex size-5 items-center justify-center rounded-full border ${item.done ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-400" : "text-muted-foreground"}`}>{item.done && <Check className="size-3" />}</span>{item.label}</li>)}</ul></section>
        <section className="rounded-2xl border border-primary/20 bg-primary/5 p-5"><h2 className="text-sm font-semibold">Trust without oversharing</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">Public visitors see your overview, hiring history, and verification badges. Legal details and uploaded documents stay private.</p></section>
      </aside>
    </div>
  </>;
}

function toValues(profile: ClientProfile): ClientProfileFormValues { return { accountType: profile.accountType, companyName: profile.companyName, overview: profile.overview, industry: profile.industry, location: profile.location, website: profile.website, companySize: profile.companySize }; }
function Section({ title, description, children }: { title: string; description: string; children: ReactNode }) { return <section className="space-y-5 rounded-2xl border bg-card/70 p-5 sm:p-6"><div><h2 className="text-lg font-semibold">{title}</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p></div>{children}</section>; }
function Field({ id, label, hint, error, children }: { id: string; label: string; hint?: string; error?: string; children: ReactNode }) { return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{children}{hint && <p className="text-xs leading-5 text-muted-foreground">{hint}</p>}{error && <p role="alert" className="text-xs text-destructive">{error}</p>}</div>; }
function Loading() { return <div className="animate-pulse space-y-6"><div className="h-32 rounded-2xl bg-muted/30" /><div className="h-96 rounded-2xl border bg-muted/20" /></div>; }
function Notice({ message, onRetry }: { message: string; onRetry: () => void }) { return <section className="rounded-2xl border border-destructive/30 bg-destructive/5 p-6"><h1 className="font-semibold">Could not load your client profile</h1><p className="mt-2 text-sm text-muted-foreground">{message}</p><Button className="mt-5" onClick={onRetry}><RefreshCw className="size-4" />Try again</Button></section>; }
