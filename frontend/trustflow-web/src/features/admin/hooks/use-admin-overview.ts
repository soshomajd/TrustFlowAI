"use client";

import { useQuery } from "@tanstack/react-query";
import { getAdminOverview } from "@/features/admin/api/admin-overview-api";
import { useAuthStore } from "@/stores/auth-store";

export function useAdminOverview() {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);

  return useQuery({
    queryKey: ["admin", "overview", user?.id],
    queryFn: ({ signal }) => getAdminOverview(signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0,
    refetchOnWindowFocus: false,
    retry: false,
  });
}
