import type { AdminProjectQuery } from "@/features/admin/types/admin-project";
import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectFilters = {
  search: string;
  status: string;
  clientId: string;
  freelancerId: string;
  from: string;
  through: string;
  pageSize: string;
};

export const emptyProjectFilters: ProjectFilters = {
  search: "", status: "", clientId: "", freelancerId: "", from: "", through: "", pageSize: "20",
};

export function buildAdminProjectQuery(filters: ProjectFilters): AdminProjectQuery {
  const search = filters.search.trim();
  if (search && search.length < 3) throw new Error("Search needs at least 3 characters.");
  if (search.length > 200) throw new Error("Search must be 200 characters or fewer.");
  const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  for (const field of ["clientId", "freelancerId"] as const)
    if (filters[field].trim() && !guid.test(filters[field].trim())) throw new Error("Enter a complete client or freelancer ID.");
  if (filters.status && !["Open", "InProgress", "Completed", "Cancelled"].includes(filters.status)) throw new Error("Choose a valid status.");
  const pageSize = Number(filters.pageSize);
  if (![20, 50, 100].includes(pageSize)) throw new Error("Choose a valid page size.");
  const date = (text: string) => {
    const value = new Date(`${text}T00:00:00.000Z`);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(text) || !Number.isFinite(value.getTime()) || value.toISOString().slice(0, 10) !== text)
      throw new Error("Choose a valid date.");
    return value;
  };
  const from = filters.from ? date(filters.from) : undefined;
  const through = filters.through ? date(filters.through) : undefined;
  if (from && through && from > through) throw new Error("From must not be after Through.");
  // The API upper bound is exclusive: include every fraction of the final UTC day.
  if (through) through.setUTCDate(through.getUTCDate() + 1);
  if (through && through.getUTCFullYear() > 9999) throw new Error("Choose an earlier final date.");
  return {
    page: 1, pageSize, search: search || undefined,
    status: (filters.status || undefined) as ProjectStatus | undefined,
    clientId: filters.clientId.trim() || undefined, freelancerId: filters.freelancerId.trim() || undefined,
    from: from?.toISOString(), to: through?.toISOString(),
  };
}
