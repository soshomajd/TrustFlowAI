"use client";
import { useQuery } from "@tanstack/react-query";
import { getAdminTimeline } from "@/features/admin/api/admin-timeline-api";
import type { TimelineFilters } from "@/features/admin/types/admin-timeline";
import { useAuthStore } from "@/stores/auth-store";
export function useAdminTimeline(projectId: string, filters: TimelineFilters, cursor?: string, refresh = 0) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "timeline", user?.id, projectId, filters, cursor, refresh],
    queryFn: ({ signal }) => getAdminTimeline(projectId, filters, cursor, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, refetchOnReconnect: false, retry: false });
}
