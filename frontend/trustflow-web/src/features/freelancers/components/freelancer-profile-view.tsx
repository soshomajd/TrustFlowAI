import { UserAvatar } from "@/features/profile-photos/components/user-avatar";
import { BriefcaseBusiness, CalendarDays, CheckCircle2, ExternalLink, MapPin, ShieldCheck, Sparkles } from "lucide-react";
import { availabilityLabels, type FreelancerProfile } from "@/features/freelancers/types/freelancer-profile";
import { isSafePortfolioUrl } from "@/features/freelancers/schemas/freelancer-profile-schema";
import { ResumeDownloadButton } from "@/features/freelancers/components/resume-download-button";

const panelClass = "rounded-2xl border bg-card/70 p-5 sm:p-7";

export function FreelancerProfileView({ profile }: { profile: FreelancerProfile }) {
  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-2xl border bg-card/80 p-6 sm:p-8">
        <div className="pointer-events-none absolute -right-16 -top-20 size-72 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex flex-col gap-5 sm:flex-row sm:items-center">
          <UserAvatar userId={profile.userId} name={profile.fullName} className="size-24 sm:size-28" />
          <div className="min-w-0 flex-1">
            <p className="mb-2 text-xs font-medium uppercase tracking-[0.2em] text-electric">Freelancer profile</p>
            <h1 className="break-words text-3xl font-semibold">{profile.fullName}</h1>
            <p className="mt-2 break-words text-lg text-muted-foreground">{profile.headline || "Still introducing their expertise"}</p>
            <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
              {profile.location && <span className="flex items-center gap-1.5"><MapPin className="size-4 shrink-0" />{profile.location}</span>}
              <span className="flex items-center gap-1.5"><CalendarDays className="size-4" />Member since {formatMonth(profile.memberSince)}</span>
            </div>
          </div>
          {profile.availability !== "NotSpecified" && (
            <span className="relative self-start rounded-full border bg-background/60 px-3 py-1.5 text-sm">
              {profile.availability === "Available" && <span className="mr-2 inline-block size-2 rounded-full bg-emerald-400" />}
              {availabilityLabels[profile.availability]}
            </span>
          )}
        </div>
      </section>

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_300px]">
        <div className="min-w-0 space-y-6">
          <section className={panelClass}>
            <h2 className="text-lg font-semibold">About</h2>
            <p className="mt-3 whitespace-pre-wrap break-words text-sm leading-7 text-muted-foreground">{profile.about || "This freelancer has not added an introduction yet. You can review their proposal for more context."}</p>
            {profile.skills.length > 0 && <div className="mt-6 border-t pt-5"><h3 className="mb-3 text-sm font-medium">Skills</h3><div className="flex flex-wrap gap-2">{profile.skills.map((skill) => <span key={skill} className="rounded-lg border border-primary/20 bg-primary/5 px-3 py-1.5 text-sm">{skill}</span>)}</div></div>}
          </section>

          <section className={panelClass}>
            <div className="flex items-center gap-2"><Sparkles className="size-5 text-electric" /><h2 className="text-lg font-semibold">Selected work</h2></div>
            <p className="mt-2 text-sm text-muted-foreground">Work samples shared by the freelancer, including work outside TrustFlow.</p>
            {profile.portfolioItems.length === 0 ? <p className="mt-5 rounded-xl border border-dashed p-5 text-sm text-muted-foreground">No work samples added yet.</p> : (
              <div className="mt-5 space-y-3">{profile.portfolioItems.map((item, index) => (
                <article key={index} className="rounded-xl border bg-background/30 p-5">
                  <h3 className="break-words font-medium">{item.title}</h3>
                  {item.description && <p className="mt-2 whitespace-pre-wrap break-words text-sm leading-6 text-muted-foreground">{item.description}</p>}
                  {item.url && isSafePortfolioUrl(item.url) && <a href={item.url} target="_blank" rel="noopener noreferrer nofollow" className="mt-4 inline-flex items-center gap-2 text-sm font-medium text-electric hover:underline">View work sample <ExternalLink className="size-3.5" /><span className="sr-only">(opens in a new tab)</span></a>}
                </article>
              ))}</div>
            )}
          </section>

          <section className={panelClass}>
            <div className="flex items-center gap-2"><BriefcaseBusiness className="size-5 text-electric" /><h2 className="text-lg font-semibold">Completed on TrustFlow</h2></div>
            <p className="mt-2 text-sm text-muted-foreground">Added automatically from completed projects on the platform.</p>
            {profile.completedProjects.length === 0 ? (
              <div className="mt-5 rounded-xl border border-dashed p-5"><p className="font-medium">Building a track record</p><p className="mt-1 text-sm leading-6 text-muted-foreground">No completed TrustFlow projects yet. Explore their skills and work samples to see whether they fit your project.</p></div>
            ) : <div className="mt-5 divide-y">{profile.completedProjects.map((project, index) => (
              <article key={index} className="flex gap-3 py-4"><CheckCircle2 className="mt-0.5 size-5 shrink-0 text-emerald-400" /><div><h3 className="break-words font-medium">{project.title}</h3><p className="mt-1 text-sm text-muted-foreground">{project.milestoneCount} {project.milestoneCount === 1 ? "milestone" : "milestones"}{project.completedAt ? ` · ${formatMonth(project.completedAt)}` : ""}</p></div></article>
            ))}</div>}
            {profile.completedProjectCount > 10 && <p className="mt-3 text-xs text-muted-foreground">Showing the 10 most recent of {profile.completedProjectCount} completed projects.</p>}
          </section>
        </div>

        <aside className="space-y-5">
          <section className={panelClass}>
            <h2 className="font-semibold">At a glance</h2>
            <dl className="mt-5 space-y-5 text-sm">
              <div><dt className="text-muted-foreground">Completed projects</dt><dd className="mt-1 text-2xl font-semibold">{profile.completedProjectCount}</dd></div>
              {profile.yearsOfExperience !== null && <div><dt className="text-muted-foreground">Experience · self-reported</dt><dd className="mt-1 font-medium">{profile.yearsOfExperience === 0 ? "Less than a year" : `${profile.yearsOfExperience} ${profile.yearsOfExperience === 1 ? "year" : "years"}`}</dd></div>}
              {profile.languages.length > 0 && <div><dt className="text-muted-foreground">Languages</dt><dd className="mt-1 break-words font-medium">{profile.languages.join(", ")}</dd></div>}
            </dl>
          </section>
          {profile.hasResume && <section className={panelClass}><h2 className="font-semibold">Resume</h2><p className="mt-2 text-sm leading-6 text-muted-foreground">PDF uploaded by the freelancer and checked before storage.</p><ResumeDownloadButton userId={profile.userId} /></section>}
          {profile.walletVerified && <section className="rounded-2xl border border-emerald-500/20 bg-emerald-500/5 p-5"><div className="flex items-center gap-2 text-sm font-medium"><ShieldCheck className="size-4 text-emerald-400" />Wallet ownership verified</div><p className="mt-2 text-xs leading-5 text-muted-foreground">This account has verified control of a wallet. This does not verify identity or professional qualifications.</p></section>}
        </aside>
      </div>
    </div>
  );
}

function formatMonth(value: string) {
  return new Intl.DateTimeFormat("en-US", { month: "short", year: "numeric", timeZone: "UTC" }).format(new Date(value));
}
