"use client";

import { Activity, ArrowRight, Gavel, LoaderCircle, RefreshCw, ShieldCheck } from "lucide-react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { useAdminOverview } from "@/features/admin/hooks/use-admin-overview";
import type { AdminOverview } from "@/features/admin/types/admin-overview";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";

const statistics = [
  ["totalProjects", "All projects", "Projects across the platform"],
  ["openProjects", "Open", "Projects marked open"],
  ["inProgressProjects", "In progress", "Projects with work in progress"],
  ["completedProjects", "Completed", "Projects marked complete"],
  ["cancelledProjects", "Cancelled", "Projects marked cancelled"],
  ["totalEscrows", "Escrow records", "Includes completed and ended contracts"],
  ["openCases", "Open cases", "Payment and refund requests awaiting resolution"],
] as const satisfies ReadonlyArray<readonly [keyof AdminOverview, string, string]>;

export function AdminOverviewScreen() {
  const query = useAdminOverview();
  const name = useAuthStore((state) => state.user?.fullName);

  return (
    <main className="mx-auto max-w-6xl space-y-7 px-4 py-8 sm:px-6">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p className="flex items-center gap-2 text-xs font-medium uppercase tracking-[0.2em] text-electric">
            <ShieldCheck className="size-4" /> Administration
          </p>
          <h1 className="mt-3 text-3xl font-semibold">Platform overview</h1>
          <p className="mt-2 text-sm text-muted-foreground">Welcome, {name ?? "administrator"}. View the current project and escrow counts.</p>
        </div>
        <Button type="button" variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>
          <RefreshCw className={query.isFetching ? "size-4 animate-spin" : "size-4"} /> Refresh overview
        </Button>
      </header>

      <div className="flex gap-3 rounded-xl border bg-card/60 p-4 text-sm text-muted-foreground">
        <Activity className="mt-0.5 size-4 shrink-0" />
        <p>Admin access is recorded. This overview shows saved platform records. An escrow can be cancelled while its project reopens, so their counts can differ.</p>
      </div>

      {query.isPending && <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground"><LoaderCircle className="size-4 animate-spin" /> Loading platform overview…</p>}
      {query.isError && <div role="alert" className="rounded-xl border border-destructive/40 bg-destructive/5 p-4 text-sm">
        <p className="font-medium">Could not load the overview</p>
        <p className="mt-1 text-muted-foreground">{getApiErrorMessage(query.error, "Please try Refresh overview.")}</p>
        {query.data && <p className="mt-2 text-muted-foreground">The counts below are from the last successful refresh.</p>}
      </div>}

      {query.data && <>
        <section aria-label="Platform statistics" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {statistics.map(([key, label, description]) => <article key={key} className="rounded-2xl border bg-card/70 p-5">
            <h2 className="text-sm font-medium text-muted-foreground">{label}</h2>
            <p className="mt-3 text-3xl font-semibold tabular-nums">{Number(query.data[key]).toLocaleString("en-US")}</p>
            <p className="mt-2 text-xs leading-5 text-muted-foreground">{description}</p>
          </article>)}
        </section>
        <p className="text-xs text-muted-foreground">Last refreshed: {new Date(query.data.generatedAt).toLocaleString()}</p>
        {query.data.totalProjects === 0 && <p className="rounded-xl border border-dashed p-5 text-sm text-muted-foreground">No projects have been created yet.</p>}
      </>}

      <section className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border bg-card/60 p-5">
        <div><h2 className="font-semibold">Project records</h2><p className="mt-1 text-sm text-muted-foreground">Find projects by title, participant, status or creation date.</p></div>
        <Button asChild variant="outline"><Link href="/dashboard/admin/projects">Browse projects <ArrowRight className="size-4" /></Link></Button>
      </section>

      <section className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border bg-card/60 p-5">
        <div className="flex items-start gap-3"><Gavel className="mt-1 size-5 text-electric" /><div>
          <h2 className="font-semibold">Review desk</h2>
          <p className="mt-1 text-sm text-muted-foreground">Read open payment and refund cases. Settlement follows the existing contract and reviewer-wallet rules.</p>
        </div></div>
        <Button asChild variant="outline"><Link href="/dashboard/review">Open review desk <ArrowRight className="size-4" /></Link></Button>
      </section>
    </main>
  );
}
