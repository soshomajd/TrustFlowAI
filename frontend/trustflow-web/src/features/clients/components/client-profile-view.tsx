import { UserAvatar } from "@/features/profile-photos/components/user-avatar";
import {
  Building2,
  CalendarDays,
  CheckCircle2,
  ExternalLink,
  FolderKanban,
  MapPin,
  ShieldCheck,
  UserRound,
  UsersRound,
  WalletCards,
} from "lucide-react";
import { companySizeLabels, type ClientProfile } from "@/features/clients/types/client-profile";

const panelClass = "rounded-2xl border bg-card/70 p-5 sm:p-7";

export function ClientProfileView({ profile }: { profile: ClientProfile }) {
  const publicName = profile.accountType === "Company" && profile.companyName
    ? profile.companyName
    : profile.displayName;

  return (
    <div className="space-y-6">
      <section className="relative overflow-hidden rounded-2xl border bg-card/80 p-6 sm:p-8">
        <div className="pointer-events-none absolute -right-16 -top-20 size-72 rounded-full bg-primary/10 blur-3xl" />
        <div className="relative flex flex-col gap-5 sm:flex-row sm:items-center">
          <UserAvatar userId={profile.userId} name={profile.displayName} className="size-24 sm:size-28" />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-xs font-medium uppercase tracking-[0.2em] text-electric">Client profile</p>
            </div>
            <h1 className="mt-3 break-words text-3xl font-semibold">{publicName}</h1>
            {profile.accountType === "Company" && <p className="mt-2 text-sm text-muted-foreground">Company account · represented by {profile.displayName}</p>}
            <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
              {profile.location && <span className="flex items-center gap-1.5"><MapPin className="size-4" />{profile.location}</span>}
              <span className="flex items-center gap-1.5"><CalendarDays className="size-4" />Member since {formatMonth(profile.memberSince)}</span>
              <span className="flex items-center gap-1.5">{profile.accountType === "Company" ? <Building2 className="size-4" /> : <UserRound className="size-4" />}{profile.accountType}</span>
            </div>
          </div>
        </div>
      </section>

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_310px]">
        <div className="space-y-6">
          <section className={panelClass}>
            <h2 className="text-lg font-semibold">About this client</h2>
            <p className="mt-3 whitespace-pre-wrap break-words text-sm leading-7 text-muted-foreground">{profile.overview || "This client has not added an introduction yet. Review the project details for more context."}</p>
            {(profile.industry || profile.website) && <div className="mt-6 grid gap-4 border-t pt-5 sm:grid-cols-2">
              {profile.industry && <div><p className="text-xs text-muted-foreground">Industry</p><p className="mt-1 break-words text-sm font-medium">{profile.industry}</p></div>}
              {profile.website && <div><p className="text-xs text-muted-foreground">Website</p><a href={profile.website} target="_blank" rel="noopener noreferrer nofollow" className="mt-1 inline-flex max-w-full items-center gap-1.5 break-all text-sm font-medium text-electric hover:underline">Visit website <ExternalLink className="size-3.5 shrink-0" /><span className="sr-only">(opens in a new tab)</span></a></div>}
            </div>}
          </section>

          <section className={panelClass}>
            <div className="flex items-center gap-2"><FolderKanban className="size-5 text-electric" /><h2 className="text-lg font-semibold">Hiring activity on TrustFlow</h2></div>
            <p className="mt-2 text-sm text-muted-foreground">These numbers come from TrustFlow project records.</p>
            <dl className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Stat label="Projects posted" value={profile.projectsPosted} />
              <Stat label="Active projects" value={profile.activeProjects} />
              <Stat label="Completed" value={profile.completedProjects} />
              <Stat label="Freelancers hired" value={profile.totalHires} />
            </dl>
          </section>
        </div>

        <aside className="space-y-5">
          <section className={panelClass}>
            <h2 className="font-semibold">Account details</h2>
            <dl className="mt-5 space-y-5 text-sm">
              <div><dt className="flex items-center gap-2 text-muted-foreground"><UsersRound className="size-4" />Team size</dt><dd className="mt-1 font-medium">{profile.accountType === "Individual" ? "Individual client" : companySizeLabels[profile.companySize]}</dd></div>
              <TrustRow icon={WalletCards} label="Wallet ownership" active={profile.walletVerified} activeText="Verified" inactiveText="Not verified" />
            </dl>
          </section>
          <section className="rounded-2xl border border-primary/20 bg-primary/5 p-5">
            <div className="flex gap-3"><CheckCircle2 className="mt-0.5 size-5 shrink-0 text-electric" /><div><h2 className="text-sm font-semibold">Know what the badges mean</h2><p className="mt-2 text-xs leading-5 text-muted-foreground">Wallet ownership means this member proved control of a wallet. It does not confirm their identity or guarantee payment.</p></div></div>
          </section>
        </aside>
      </div>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number }) {
  return <div className="rounded-xl border bg-background/30 p-4"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-2 text-2xl font-semibold">{value}</dd></div>;
}

function TrustRow({ icon: Icon, label, active, activeText, inactiveText }: { icon: typeof ShieldCheck; label: string; active: boolean; activeText: string; inactiveText: string }) {
  return <div><dt className="flex items-center gap-2 text-muted-foreground"><Icon className="size-4" />{label}</dt><dd className={`mt-1 font-medium ${active ? "text-emerald-400" : ""}`}>{active ? activeText : inactiveText}</dd></div>;
}

function formatMonth(value: string) {
  return new Intl.DateTimeFormat("en-US", { month: "short", year: "numeric", timeZone: "UTC" }).format(new Date(value));
}
