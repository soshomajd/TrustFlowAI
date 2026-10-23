"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useAdminTimeline } from "@/features/admin/hooks/use-admin-timeline";
import type { TimelineCategory, TimelineFilters, TimelinePage } from "@/features/admin/types/admin-timeline";
import type { DossierSection } from "@/features/admin/types/admin-dossier";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

const categories: { value: TimelineCategory; label: string }[] = [
  { value: "all", label: "All project events" }, { value: "project", label: "Project" },
  { value: "milestones", label: "Milestones" }, { value: "proposals", label: "Proposals" },
  { value: "escrows", label: "Escrow lifecycle" }, { value: "deliveries", label: "Deliveries and files" },
  { value: "conversations", label: "Conversation activity" }, { value: "disputes", label: "Disputes and AI opinions" },
  { value: "admin", label: "Administrator access" },
];
const control = "rounded-md border bg-background px-3 py-2 text-sm";

export function AdminTimelineEvents({ data, projectId, onInspect, onActor }: {
  data: TimelinePage; projectId: string; onInspect?: (section: DossierSection) => void; onActor?: (id: string) => void;
}) {
  if (data.projectId !== projectId) return <p role="alert">These events belong to another project. Refresh latest events.</p>;
  return <>{data.items.length ? <ol className="space-y-4" aria-label="Saved project events">{data.items.map((event) =>
    <li key={`${event.code}:${event.resourceId}`} className="rounded-xl border bg-card/60 p-5">
      <p className="text-xs text-muted-foreground">{categories.find((item) => item.value === event.category)?.label}</p>
      <h3 className="mt-1 font-semibold">{event.title}</h3>
      {event.occurredAt ? <time className="mt-2 block text-sm" dateTime={event.occurredAt}>{new Date(event.occurredAt).toLocaleString()}</time> : <p className="mt-2 text-sm font-medium">Time not recorded — not placed in the dated sequence</p>}
      {event.label && <p dir="auto" className="mt-3 whitespace-pre-wrap wrap-anywhere text-sm">{event.category === "admin" ? event.label.replace(/^Admin/, "").replace(/([a-z])([A-Z])/g, "$1 $2") : event.label}</p>}
      <dl className="mt-4 grid gap-3 text-sm sm:grid-cols-2">
        <div><dt className="text-xs text-muted-foreground">Recorded actor</dt><dd className="wrap-anywhere">{event.actorName ?? "Not recorded; no actor inferred"}</dd></div>
        <div><dt className="text-xs text-muted-foreground">Resource reference</dt><dd className="break-all">{event.resourceId}</dd></div>
        {event.relatedId && <div><dt className="text-xs text-muted-foreground">Related record</dt><dd className="break-all">{event.relatedId}</dd></div>}
        {event.transactionHash && <div><dt className="text-xs text-muted-foreground">Saved transaction reference</dt><dd className="break-all">{event.transactionHash}</dd></div>}
        {event.transactionSender && <div><dt className="text-xs text-muted-foreground">Transaction sender wallet</dt><dd className="break-all">{event.transactionSender}</dd></div>}
        {event.blockNumber != null && <div><dt className="text-xs text-muted-foreground">Block number</dt><dd>{event.blockNumber}</dd></div>}
      </dl>
      <div className="mt-4 flex flex-wrap gap-2">
        {onInspect && event.category !== "admin" && event.category !== "project" && event.category !== "all" && <Button variant="outline" size="sm" onClick={() => onInspect(event.category as DossierSection)}>Open related section</Button>}
        {onActor && event.actorId && <Button variant="outline" size="sm" onClick={() => onActor(event.actorId!)}>Filter this actor</Button>}
      </div>
    </li>)}</ol> : <p className="rounded-xl border border-dashed p-6">No saved events match these filters.</p>}</>;
}

function TimelineHistory({ projectId, filters, onInspect, onActor }: {
  projectId: string; filters: TimelineFilters; onInspect?: (section: DossierSection) => void; onActor: (id: string) => void;
}) {
  const [cursors, setCursors] = useState<(string | undefined)[]>([undefined]);
  const [index, setIndex] = useState(0);
  const [refresh, setRefresh] = useState(0);
  const query = useAdminTimeline(projectId, filters, cursors[index], refresh);
  return <div className="space-y-4">
    <Button variant="outline" disabled={query.isFetching} onClick={() => { setCursors([undefined]); setIndex(0); setRefresh((old) => old + 1); }}>Refresh latest events</Button>
    {query.isPending && <p role="status">Loading project history…</p>}
    {query.isError && <div role="alert" className="rounded-xl border border-destructive/40 p-4">
      <p>Could not load project history.</p><p className="mt-2 text-sm">{getApiErrorMessage(query.error, "Try Refresh latest events.")}</p>
      {query.data && <p className="mt-2 text-sm">Events below are from the last successful refresh.</p>}
      <Button className="mt-3" variant="outline" onClick={() => void query.refetch()}>Retry history</Button>
    </div>}
    {query.data && <>
      <p className="text-sm text-muted-foreground">History page {index + 1} · access cutoff {new Date(query.data.snapshotAt).toLocaleString()}. Later admin views are excluded until refresh. Saved metadata can change.</p>
      <AdminTimelineEvents data={query.data} projectId={projectId} onInspect={onInspect} onActor={onActor} />
      <nav aria-label="Timeline pages" className="flex justify-between gap-3">
        <Button variant="outline" disabled={index === 0 || query.isFetching} onClick={() => setIndex((old) => old - 1)}>Newer events</Button>
        <Button variant="outline" disabled={!query.data.nextCursor || query.isFetching} onClick={() => {
          const next = query.data?.nextCursor;
          if (next) { setCursors((old) => [...old.slice(0, index + 1), next]); setIndex((old) => old + 1); }
        }}>Older events</Button>
      </nav>
    </>}
  </div>;
}

export function AdminTimeline({ projectId, onInspect }: { projectId: string; onInspect?: (section: DossierSection) => void }) {
  const [filters, setFilters] = useState<TimelineFilters>({ category: "all", timing: "recorded" });
  const [formError, setFormError] = useState<string>();
  return <section aria-label="Project timeline" className="space-y-5">
    <h2 className="text-xl font-semibold">Timeline and admin access</h2>
    <p className="rounded-xl border p-4 text-sm text-muted-foreground">Confidential, read-only saved metadata. Views and displayed references are recorded. Choose Administrator access to inspect admin views, downloads and exports separately. No message bodies, file contents or private AI prompts are copied here. Events labelled chain time use verified block timestamps; other dates may be saved recording times. This view does not check the blockchain, move funds or mark participant messages read. AI opinions are advice. Current status, deadlines and later update times are not historical event times.</p>
    <form className="flex flex-wrap items-end gap-3" onSubmit={(event) => {
      event.preventDefault(); const data = new FormData(event.currentTarget);
      const timing = data.get("timing") as TimelineFilters["timing"];
      const from = timing === "recorded" && data.get("from") ? `${data.get("from")}T00:00:00Z` : undefined;
      const to = timing === "recorded" && data.get("to") ? `${data.get("to")}T23:59:59.999999Z` : undefined;
      if (from && to && from > to) { setFormError("The start date must be on or before the end date."); return; }
      setFormError(undefined); setFilters({ category: data.get("category") as TimelineCategory, timing, from, to, actorId: filters.actorId });
    }}>
      <label className="grid gap-1 text-sm">Event type<select className={control} name="category" defaultValue="all">{categories.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}</select></label>
      <label className="grid gap-1 text-sm">Time evidence<select className={control} name="timing" defaultValue="recorded"><option value="recorded">Recorded times</option><option value="missing">Missing times (separate records)</option></select></label>
      <label className="grid gap-1 text-sm">From (UTC)<input className={control} name="from" type="date" /></label>
      <label className="grid gap-1 text-sm">Through (UTC)<input className={control} name="to" type="date" /></label>
      <Button type="submit">Apply filters</Button>
    </form>
    {formError && <p role="alert">{formError}</p>}
    {filters.timing === "missing" && <p className="text-sm text-muted-foreground">These are gaps in saved metadata, not dated events. Dates cannot be reconstructed from the current status. No records are deleted by this filter.</p>}
    {filters.actorId && <p className="flex flex-wrap items-center gap-3 text-sm">Showing the selected recorded actor <span className="break-all">{filters.actorId}</span><Button variant="outline" size="sm" onClick={() => setFilters((old) => ({ ...old, actorId: undefined }))}>Clear actor filter</Button></p>}
    <TimelineHistory key={`${projectId}:${JSON.stringify(filters)}`} projectId={projectId} filters={filters} onInspect={onInspect} onActor={(actorId) => setFilters((old) => ({ ...old, actorId }))} />
  </section>;
}
