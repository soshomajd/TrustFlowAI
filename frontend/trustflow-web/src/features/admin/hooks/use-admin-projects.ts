"use client";

import { useQuery } from "@tanstack/react-query";
import { getAdminProjects } from "@/features/admin/api/admin-projects-api";
import type { AdminProjectQuery } from "@/features/admin/types/admin-project";
import { useAuthStore } from "@/stores/auth-store";

export function useAdminProjects(filters: AdminProjectQuery) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({
    queryKey: ["admin", "projects", user?.id, filters],
    queryFn: ({ signal }) => getAdminProjects(filters, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0,
    refetchOnWindowFocus: false,
    retry: false,
  });
}
