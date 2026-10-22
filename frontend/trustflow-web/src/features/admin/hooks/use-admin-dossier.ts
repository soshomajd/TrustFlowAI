"use client";
import { useQuery } from "@tanstack/react-query";
import { getAdminDossier, getAdminDossierRecords } from "@/features/admin/api/admin-dossier-api";
import type { DossierSection } from "@/features/admin/types/admin-dossier";
import { useAuthStore } from "@/stores/auth-store";

export function useAdminDossier(projectId: string) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "dossier", user?.id, projectId],
    queryFn: ({ signal }) => getAdminDossier(projectId, signal),
    enabled: status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
export function useAdminDossierRecords(projectId: string, section: DossierSection, page: number, ready: boolean) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  return useQuery({ queryKey: ["admin", "dossier-records", user?.id, projectId, section, page],
    queryFn: ({ signal }) => getAdminDossierRecords(projectId, section, page, signal),
    enabled: ready && status === "authenticated" && Boolean(user?.roles.includes("Admin")),
    staleTime: 0, refetchOnWindowFocus: false, retry: false });
}
