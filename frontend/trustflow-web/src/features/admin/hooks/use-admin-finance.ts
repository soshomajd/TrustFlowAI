"use client";
import { useQuery } from "@tanstack/react-query";
import { getAdminFinance, getAdminFinanceEntries } from "@/features/admin/api/admin-finance-api";
import type { FinanceSection } from "@/features/admin/types/admin-finance";
import { useAuthStore } from "@/stores/auth-store";
export function useAdminFinance(projectId: string, page: number) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "finance", user?.id, projectId, page],
    queryFn: ({ signal }) => getAdminFinance(projectId, page, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
export function useAdminFinanceEntries(projectId: string, escrowId: string | undefined, section: FinanceSection, page: number) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "finance-entries", user?.id, projectId, escrowId, section, page],
    queryFn: ({ signal }) => getAdminFinanceEntries(projectId, escrowId!, section, page, signal),
    enabled: Boolean(escrowId) && status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
