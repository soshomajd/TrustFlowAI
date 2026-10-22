"use client";
import { useQuery } from "@tanstack/react-query";
import { getAdminDisputes, getAdminDispute } from "@/features/admin/api/admin-disputes-api";
import type { AdminDisputeKind } from "@/features/admin/types/admin-disputes";
import { useAuthStore } from "@/stores/auth-store";
export function useAdminDisputes(projectId: string, kind: AdminDisputeKind, page: number) {
  const user = useAuthStore((state) => state.user); const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "disputes", user?.id, projectId, kind, page],
    queryFn: ({ signal }) => getAdminDisputes(projectId, kind, page, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")), staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
export function useAdminDispute(projectId: string, kind: AdminDisputeKind, id: string) {
  const user = useAuthStore((state) => state.user); const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "dispute-detail", user?.id, projectId, kind, id],
    queryFn: ({ signal }) => getAdminDispute(projectId, kind, id, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")), staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
