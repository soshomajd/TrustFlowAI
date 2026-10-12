"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { toast } from "sonner";
import { Check, Eye, LoaderCircle, Plus, RefreshCw, Save, Trash2 } from "lucide-react";
import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ProfileLoading, ProfileNotice } from "@/features/freelancers/components/freelancer-profile-screen";
import { useMyFreelancerProfile, useUpdateFreelancerProfile } from "@/features/freelancers/hooks/use-freelancer-profile";
import { freelancerProfileSchema, splitEntries, type FreelancerProfileFormValues } from "@/features/freelancers/schemas/freelancer-profile-schema";
import { availabilityLabels, type Availability, type FreelancerProfile } from "@/features/freelancers/types/freelancer-profile";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { ProfilePhotoEditor } from "@/features/profile-photos/components/profile-photo-editor";
import { UserAvatar } from "@/features/profile-photos/components/user-avatar";
import { ResumeManager } from "@/features/freelancers/components/resume-manager";
import { FreelancerAiCoach } from "@/features/ai/components/freelancer-ai-coach";

export function EditFreelancerProfileScreen() {
  const query = useMyFreelancerProfile();
  return (
    <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6">
      {query.isPending ? <ProfileLoading /> : query.isError ? (
        <ProfileNotice title="Could not load your profile" message={getApiErrorMessage(query.error)}><Button onClick={() => void query.refetch()}><RefreshCw className="size-4" />Try again</Button></ProfileNotice>
      ) : <ProfileEditor key={query.data.userId} profile={query.data} />}
    </main>
  );
}

function toFormValues(profile: FreelancerProfile): FreelancerProfileFormValues {
  return {
    headline: profile.headline,
    about: profile.about,
    location: profile.location,
    yearsOfExperience: profile.yearsOfExperience?.toString() ?? "",
    availability: profile.availability,
    skills: profile.skills.join(", "),
    languages: profile.languages.join(", "),
    portfolioItems: profile.portfolioItems,
  };
}

function ProfileEditor({ profile }: { profile: FreelancerProfile }) {
  const mutation = useUpdateFreelancerProfile();
  const form = useForm<FreelancerProfileFormValues>({
    resolver: zodResolver(freelancerProfileSchema),
    defaultValues: toFormValues(profile),
  });
  const { fields, append, remove } = useFieldArray({ control: form.control, name: "portfolioItems" });
  const [headline, about, skills] = useWatch({ control: form.control, name: ["headline", "about", "skills"] });
  const essentials = [
    { label: "Professional title", done: Boolean(headline.trim()) },
    { label: "Short introduction", done: Boolean(about.trim()) },
    { label: "Your skills", done: splitEntries(skills).length > 0 },
  ];
  const completed = essentials.filter((item) => item.done).length;
  const { errors, isDirty } = form.formState;

  const optionalFieldNames = ["location", "languages", "yearsOfExperience", "availability"] as const;
  const hasOptionalSectionError = optionalFieldNames.some((name) => errors[name]);
  const [wasOptionalSectionOpened, setOptionalSectionOpened] = useState(false);
  // Never leave an error hidden behind a collapsed section.
  const isOptionalSectionOpen = wasOptionalSectionOpened || hasOptionalSectionError;

  function setOptionalSectionOpen(open: boolean) {
    setOptionalSectionOpened(open || hasOptionalSectionError);
  }

  function showFirstProblem() {
    toast.error("Some details still need fixing. The first one is highlighted.");
  }

  useEffect(() => {
    if (!isDirty) return;
    function warnBeforeLeaving(event: BeforeUnloadEvent) { event.preventDefault(); }
    window.addEventListener("beforeunload", warnBeforeLeaving);
    return () => window.removeEventListener("beforeunload", warnBeforeLeaving);
  }, [isDirty]);

  function save(values: FreelancerProfileFormValues) {
    mutation.mutate({
      ...values,
      skills: splitEntries(values.skills),
      languages: splitEntries(values.languages),
      yearsOfExperience: values.yearsOfExperience === "" ? null : Number(values.yearsOfExperience),
    }, { onSuccess: (saved) => form.reset(toFormValues(saved)) });
  }

  return (
    <>
      <header className="mb-8 flex flex-col gap-5 sm:flex-row sm:items-center sm:justify-between">
        <div><p className="text-xs font-medium uppercase tracking-[0.2em] text-electric">Your professional introduction</p><h1 className="mt-3 text-3xl font-semibold">My profile</h1><p className="mt-3 max-w-xl text-sm leading-6 text-muted-foreground">Help clients understand what you do and why you fit their project. Start small and add more whenever you like.</p></div>
        <Button asChild variant="outline" className="self-start sm:shrink-0"><Link href={`/dashboard/freelancers/${profile.userId}`}><Eye className="size-4" />View saved profile</Link></Button>
      </header>
      <ProfilePhotoEditor userId={profile.userId} name={profile.fullName} />
      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_260px]">
        <form onSubmit={form.handleSubmit(save, showFirstProblem)} className="min-w-0 space-y-6 pb-28" noValidate>
          <fieldset disabled={mutation.isPending} className="min-w-0 space-y-6">
            <EditorSection number="01" title="The essentials" description="A few lines are enough to make a useful introduction.">
              <div className="flex items-center gap-3 rounded-xl bg-background/40 p-3"><UserAvatar userId={profile.userId} name={profile.fullName} /><div><p className="text-sm font-medium">{profile.fullName}</p><p className="text-xs text-muted-foreground">Your name from your TrustFlow account</p></div></div>
              <Field id="headline" label="Professional title" error={errors.headline?.message} hint="One line that says what you do.">
                <Input id="headline" {...form.register("headline")} maxLength={100} placeholder="e.g. Frontend developer for fast, accessible websites" aria-describedby="headline-help" aria-invalid={Boolean(errors.headline)} />
              </Field>
              <Field id="about" label="Short introduction" error={errors.about?.message} hint={`${about.length}/1200 characters. Tell clients what you build, who you help and how you work.`}>
                <Textarea id="about" {...form.register("about")} maxLength={1200} rows={5} className="min-h-36" placeholder="I help small businesses turn ideas into easy-to-use websites. My focus is clean interfaces, clear communication and reliable delivery." aria-describedby="about-help" aria-invalid={Boolean(errors.about)} />
              </Field>
              <Field id="skills" label="Skills" error={errors.skills?.message} hint="Up to 10 skills, separated by commas. Keep the ones most relevant to your work.">
                <Input id="skills" {...form.register("skills")} maxLength={1000} placeholder="React, UI design, TypeScript" aria-describedby="skills-help" aria-invalid={Boolean(errors.skills)} />
              </Field>
              {splitEntries(skills).length > 0 && <div className="flex flex-wrap gap-2">{splitEntries(skills).slice(0, 10).map((skill) => <span key={skill} className="max-w-full break-words rounded-lg border border-primary/20 bg-primary/5 px-2.5 py-1 text-xs">{skill}</span>)}</div>}
              <SectionSaveAction isDirty={isDirty} isPending={mutation.isPending} />
            </EditorSection>

            <details open={isOptionalSectionOpen} onToggle={(event) => setOptionalSectionOpen(event.currentTarget.open)} className="rounded-2xl border bg-card/70 p-5 sm:p-6">
              <summary className="cursor-pointer font-semibold">A little more about you <span className="ml-2 text-xs font-normal text-muted-foreground">Optional</span></summary>
              <div className="mt-6 space-y-5">
                <p className="text-sm text-muted-foreground">Help clients plan communication and timing. Share only what you want them to see.</p>
                <Field id="location" label="Location" error={errors.location?.message} hint="City or country is enough; no street address needed."><Input id="location" {...form.register("location")} maxLength={100} placeholder="e.g. Tehran, Iran" aria-describedby="location-help" /></Field>
                <Field id="languages" label="Languages" error={errors.languages?.message} hint="Up to 5 languages, separated by commas."><Input id="languages" {...form.register("languages")} maxLength={1000} placeholder="e.g. Persian, English" aria-describedby="languages-help" /></Field>
                <Field id="yearsOfExperience" label="Years of professional experience" error={errors.yearsOfExperience?.message} hint="Use 0 for less than a year, or leave blank."><Input id="yearsOfExperience" {...form.register("yearsOfExperience")} type="number" min={0} max={60} step={1} placeholder="e.g. 3" aria-describedby="yearsOfExperience-help" /></Field>
                <Field id="availability" label="Availability" error={errors.availability?.message}>
                  <select id="availability" {...form.register("availability")} className="h-10 w-full rounded-lg border bg-background px-3 text-sm" aria-describedby="availability-help">{(Object.keys(availabilityLabels) as Availability[]).map((value) => <option key={value} value={value}>{availabilityLabels[value]}</option>)}</select>
                </Field>
                <SectionSaveAction isDirty={isDirty} isPending={mutation.isPending} />
              </div>
            </details>

            <EditorSection number="02" title="Selected work" description="Optional. Add up to 3 examples you are proud of, from TrustFlow or elsewhere.">
              {fields.length === 0 && <p className="rounded-xl border border-dashed p-5 text-sm leading-6 text-muted-foreground">A single good example can say a lot. Add a short description, and a link if you have one.</p>}
              {fields.map((field, index) => (
                <div key={field.id} className="space-y-4 rounded-xl border bg-background/30 p-4">
                  <div className="flex items-center justify-between"><h3 className="text-sm font-semibold">Work sample {index + 1}</h3><Button type="button" variant="ghost" size="sm" onClick={() => remove(index)} aria-label={`Remove work sample ${index + 1}`}><Trash2 className="size-4" />Remove</Button></div>
                  <Field id={`work-title-${index}`} label="Project title" error={errors.portfolioItems?.[index]?.title?.message}><Input id={`work-title-${index}`} {...form.register(`portfolioItems.${index}.title`)} maxLength={100} placeholder="e.g. Online booking website" aria-describedby={`work-title-${index}-help`} aria-invalid={Boolean(errors.portfolioItems?.[index]?.title)} /></Field>
                  <Field id={`work-description-${index}`} label="Your role and result" error={errors.portfolioItems?.[index]?.description?.message} hint="A sentence or two: what you did and what it achieved."><Textarea id={`work-description-${index}`} {...form.register(`portfolioItems.${index}.description`)} maxLength={500} rows={3} placeholder="I designed and built a mobile-friendly booking flow that made scheduling easier for customers." aria-describedby={`work-description-${index}-help`} /></Field>
                  <Field id={`work-url-${index}`} label="Link (optional)" error={errors.portfolioItems?.[index]?.url?.message} hint="A live project, GitHub repository, case study or portfolio page."><Input id={`work-url-${index}`} {...form.register(`portfolioItems.${index}.url`)} maxLength={500} type="url" placeholder="https://..." aria-describedby={`work-url-${index}-help`} aria-invalid={Boolean(errors.portfolioItems?.[index]?.url)} /></Field>
                </div>
              ))}
              {fields.length < 3 && <Button type="button" variant="outline" onClick={() => append({ title: "", description: "", url: "" })}><Plus className="size-4" />Add work sample</Button>}
              {errors.portfolioItems?.root?.message && <p role="alert" className="text-sm text-destructive">{errors.portfolioItems.root.message}</p>}
              <SectionSaveAction isDirty={isDirty} isPending={mutation.isPending} />
            </EditorSection>
          </fieldset>

          {mutation.isError && <p role="alert" className="rounded-xl border border-destructive/30 bg-destructive/5 p-4 text-sm text-destructive">{getApiErrorMessage(mutation.error, "Your profile could not be saved. Your edits are still here; please try again.")}</p>}
          <div className="pointer-events-none sticky bottom-4 flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-background/95 p-4 shadow-lg backdrop-blur">
            <p role="status" className="pointer-events-auto text-xs text-muted-foreground">{mutation.isPending ? "Saving your profile…" : isDirty ? "You have unsaved changes" : profile.updatedAt ? "All changes saved" : "Save whenever you are ready"}</p>
            <div className="pointer-events-auto"><SaveChangesButton isDirty={isDirty} isPending={mutation.isPending} /></div>
          </div>
        </form>

        <aside className="space-y-5 lg:sticky lg:top-24">
          <FreelancerAiCoach
            portfolioSlotsLeft={3 - fields.length}
            onApplyHeadline={(value) => form.setValue("headline", value, { shouldDirty: true, shouldValidate: true })}
            onApplyAbout={(value) => form.setValue("about", value, { shouldDirty: true, shouldValidate: true })}
            onApplySkills={(values) => form.setValue("skills", values.join(", "), { shouldDirty: true, shouldValidate: true })}
            onAddPortfolioItem={(item) => append({ title: item.title, description: item.description, url: "" })}
          />
          <ResumeManager profile={profile} />
          <section className="rounded-2xl border bg-card/70 p-5">
            <h2 className="font-semibold">A clear first impression</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">Start with these three things. Everything else can wait.</p>
            <div className="mt-5 h-1.5 overflow-hidden rounded-full bg-muted" role="progressbar" aria-label="Profile essentials" aria-valuenow={completed} aria-valuemin={0} aria-valuemax={3}><div className="h-full rounded-full bg-primary transition-all" style={{ width: `${completed / 3 * 100}%` }} /></div>
            <p className="mt-2 text-xs text-muted-foreground">{completed} of 3 essentials added</p>
            <ul className="mt-5 space-y-3">{essentials.map((item) => <li key={item.label} className="flex items-center gap-2 text-sm"><span className={`flex size-5 items-center justify-center rounded-full border ${item.done ? "border-emerald-500/30 bg-emerald-500/10 text-emerald-400" : "text-muted-foreground"}`}>{item.done && <Check className="size-3" />}</span>{item.label}<span className="sr-only">{item.done ? " added" : " not added"}</span></li>)}</ul>
          </section>
          <section className="rounded-2xl border border-primary/20 bg-primary/5 p-5"><h2 className="text-sm font-semibold">Your work builds your profile</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">Completed TrustFlow projects appear automatically. You do not need to enter them again.</p><p className="mt-4 text-xs leading-5 text-muted-foreground">Clients can open this profile from your proposals. You can keep applying while you fill it in.</p></section>
        </aside>
      </div>
    </>
  );
}

function EditorSection({ number, title, description, children }: { number: string; title: string; description: string; children: ReactNode }) {
  return <section className="space-y-5 rounded-2xl border bg-card/70 p-5 sm:p-6"><div><div className="flex items-center gap-3"><span className="text-xs font-semibold text-electric">{number}</span><h2 className="text-lg font-semibold">{title}</h2></div><p className="mt-2 text-sm leading-6 text-muted-foreground">{description}</p></div>{children}</section>;
}

function SectionSaveAction({ isDirty, isPending }: { isDirty: boolean; isPending: boolean }) {
  return (
    <div className="flex items-center justify-between gap-3 border-t pt-5">
      <p className="text-xs text-muted-foreground">
        {isDirty ? "Save your profile changes when you are ready." : "No unsaved changes"}
      </p>
      <SaveChangesButton isDirty={isDirty} isPending={isPending} />
    </div>
  );
}

function SaveChangesButton({ isDirty, isPending }: { isDirty: boolean; isPending: boolean }) {
  return (
    <Button type="submit" disabled={isPending || !isDirty} className="shrink-0">
      {isPending ? <LoaderCircle className="size-4 animate-spin" /> : <Save className="size-4" />}
      {isPending ? "Saving…" : "Save changes"}
    </Button>
  );
}

function Field({ id, label, hint, error, children }: { id: string; label: string; hint?: string; error?: string; children: ReactNode }) {
  return <div className="space-y-2"><Label htmlFor={id}>{label}</Label>{children}<div id={`${id}-help`}>{hint && <p className="text-xs leading-5 text-muted-foreground">{hint}</p>}{error && <p role="alert" className="mt-1 text-xs text-destructive">{error}</p>}</div></div>;
}
