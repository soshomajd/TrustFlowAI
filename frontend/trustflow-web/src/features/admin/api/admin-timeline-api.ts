import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { TimelineFilters, TimelinePage } from "@/features/admin/types/admin-timeline";
export async function getAdminTimeline(projectId: string, filters: TimelineFilters, cursor?: string, signal?: AbortSignal) {
  return (await apiClient.get<TimelinePage>(`/admin/projects/${encodeURIComponent(projectId)}/timeline`,
    { params: { ...filters, cursor, pageSize: 25 }, signal })).data;
}
