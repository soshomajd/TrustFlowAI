"use client";

import { useState, type FormEvent } from "react";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useAdminProjects } from "@/features/admin/hooks/use-admin-projects";
import { buildAdminProjectQuery, emptyProjectFilters, type ProjectFilters } from "@/features/admin/lib/project-filters";
import type { AdminProjectQuery } from "@/features/admin/types/admin-project";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { formatMoney } from "@/lib/money/exact-money";

const statuses = { Open: "Open", InProgress: "In progress", Completed: "Completed", Cancelled: "Cancelled" };

export function AdminProjectsScreen() {
  const [draft, setDraft] = useState<ProjectFilters>(emptyProjectFilters);
  const [filters, setFilters] = useState<AdminProjectQuery>({ page: 1, pageSize: 20 });
  const [validationError, setValidationError] = useState<string>();
  const query = useAdminProjects(filters);
  const change = (key: keyof ProjectFilters, value: string) => setDraft((old) => ({ ...old, [key]: value }));
  const apply = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    try {
      setFilters(buildAdminProjectQuery(draft));
      setValidationError(undefined);
    } catch (error) {
      setValidationError(error instanceof Error ? error.message : "Check the filters.");
    }
  };

  return <main className="mx-auto max-w-7xl space-y-6 px-4 py-8 sm:px-6">
    <header className="flex flex-wrap items-start justify-between gap-4">
      <div><Link href="/dashboard/admin" className="text-sm text-electric hover:underline">Admin overview</Link>
        <h1 className="mt-3 text-3xl font-semibold">All projects</h1>
        <p className="mt-2 text-sm text-muted-foreground">Browse saved platform records. Access to this list and each displayed project is recorded.</p>
      </div>
      <Button variant="outline" disabled={query.isFetching} onClick={() => void query.refetch()}>Refresh projects</Button>
    </header>

    <form onSubmit={apply} className="grid gap-4 rounded-2xl border bg-card/60 p-5 sm:grid-cols-2 lg:grid-cols-4" aria-label="Project filters">
      <label className="space-y-2 text-sm sm:col-span-2"><span>Search</span>
        <Input value={draft.search} maxLength={200} onChange={(e) => change("search", e.target.value)} placeholder="Project title, client, freelancer or full project ID" />
      </label>
      <label className="space-y-2 text-sm"><span>Status</span>
        <select className="h-9 w-full rounded-md border bg-background px-3" value={draft.status} onChange={(e) => change("status", e.target.value)}>
          <option value="">All statuses</option>{Object.entries(statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
        </select>
      </label>
      <label className="space-y-2 text-sm"><span>Projects per page</span>
        <select className="h-9 w-full rounded-md border bg-background px-3" value={draft.pageSize} onChange={(e) => change("pageSize", e.target.value)}>
          {[20, 50, 100].map((size) => <option key={size} value={size}>{size}</option>)}
        </select>
      </label>
      <label className="space-y-2 text-sm"><span>Client ID</span><Input value={draft.clientId} onChange={(e) => change("clientId", e.target.value)} placeholder="Full account ID (optional)" /></label>
      <label className="space-y-2 text-sm"><span>Freelancer ID</span><Input value={draft.freelancerId} onChange={(e) => change("freelancerId", e.target.value)} placeholder="Includes past escrow parties" /></label>
      <label className="space-y-2 text-sm"><span>Created from (UTC)</span><Input type="date" value={draft.from} onChange={(e) => change("from", e.target.value)} /></label>
      <label className="space-y-2 text-sm"><span>Created through (UTC)</span><Input type="date" value={draft.through} onChange={(e) => change("through", e.target.value)} /></label>
      <div className="flex gap-2 sm:col-span-2 lg:col-span-4">
        <Button type="submit">Apply filters</Button>
        <Button type="button" variant="outline" onClick={() => { setDraft(emptyProjectFilters); setFilters({ page: 1, pageSize: 20 }); setValidationError(undefined); }}>Clear filters</Button>
      </div>
      {validationError && <p role="alert" className="text-sm text-destructive sm:col-span-2 lg:col-span-4">{validationError}</p>}
    </form>

    {query.isPending && <p role="status">Loading projects…</p>}
    {query.isFetching && !query.isPending && <p role="status" className="text-sm text-muted-foreground">Updating projects…</p>}
    {query.isError && <div role="alert" className="rounded-xl border border-destructive/40 p-4 text-sm">
      <p className="font-medium">Could not load projects</p><p>{getApiErrorMessage(query.error, "Try Refresh projects.")}</p>
      {query.data && <p>The results below are from the last successful refresh.</p>}
    </div>}
    {query.data && <>
      <p className="text-sm text-muted-foreground" aria-live="polite">{query.data.totalItems.toLocaleString("en-US")} matching projects · Newest first</p>
      {query.data.items.length === 0 ? <p className="rounded-xl border border-dashed p-6">No projects on this page. Change the filters or go to the previous page.</p> :
        <div className="overflow-x-auto rounded-xl border"><table className="w-full min-w-240 text-left text-sm">
          <caption className="sr-only">Admin project records</caption>
          <thead className="bg-muted/40"><tr>{["Project", "Client", "Freelancers", "Status", "Budget", "Records", "Created"].map((title) => <th key={title} scope="col" className="p-4 font-medium">{title}</th>)}</tr></thead>
          <tbody>{query.data.items.map((project) => <tr key={project.id} className="border-t align-top">
            <td className="max-w-72 p-4"><Link href={`/dashboard/admin/projects/${project.id}`} className="font-medium text-electric underline-offset-4 hover:underline focus-visible:underline wrap-anywhere">{project.title}</Link><p className="mt-1 text-xs text-muted-foreground break-all">{project.id}</p></td>
            <td className="p-4"><p>{project.clientName}</p>{project.clientId && <p className="mt-1 text-xs text-muted-foreground break-all">{project.clientId}</p>}</td>
            <td className="p-4">{project.freelancers.length ? <ul className="space-y-2">{project.freelancers.map((person) => <li key={person.id}><p>{person.fullName}</p><p className="text-xs text-muted-foreground break-all">{person.id}</p></li>)}</ul> : "No assigned or past escrow freelancer"}</td>
            <td className="whitespace-nowrap p-4">{statuses[project.status]}</td>
            <td className="p-4 tabular-nums">{formatMoney(project.budgetExact)}</td>
            <td className="whitespace-nowrap p-4 text-muted-foreground"><p>{project.milestoneCount} stages</p><p>{project.escrowCount} escrows</p><p>{project.proposalCount} proposals</p><p>{project.openCaseCount} open cases</p></td>
            <td className="whitespace-nowrap p-4">{new Date(project.createdAt).toLocaleDateString()}</td>
          </tr>)}</tbody>
        </table></div>}
      <nav aria-label="Project pages" className="flex flex-wrap items-center justify-between gap-3">
        <Button variant="outline" disabled={!query.data.hasPreviousPage || query.isFetching} onClick={() => setFilters((old) => ({ ...old, page: old.page - 1 }))}>Previous</Button>
        <p className="text-sm">Page {query.data.page} of {Math.max(1, query.data.totalPages)}</p>
        <Button variant="outline" disabled={!query.data.hasNextPage || query.isFetching} onClick={() => setFilters((old) => ({ ...old, page: old.page + 1 }))}>Next</Button>
      </nav>
      <p className="text-xs text-muted-foreground">Freelancers include current assignees and past escrow parties. Project and escrow statuses can differ.</p>
    </>}
  </main>;
}
