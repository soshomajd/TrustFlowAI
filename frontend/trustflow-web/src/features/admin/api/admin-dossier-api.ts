import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { AdminDossier, DossierRecords, DossierSection } from "@/features/admin/types/admin-dossier";

export async function getAdminDossier(projectId: string, signal?: AbortSignal) {
  return (await apiClient.get<AdminDossier>(`/admin/projects/${encodeURIComponent(projectId)}`, { signal })).data;
}
export async function getAdminDossierRecords(projectId: string, section: DossierSection, page: number, signal?: AbortSignal) {
  return (await apiClient.get<DossierRecords>(`/admin/projects/${encodeURIComponent(projectId)}/${section}`,
    { params: { page, pageSize: 20 }, signal })).data;
}

export async function downloadAdminDeliveryFile(projectId: string, submissionId: string, fileId: string) {
  return (await apiClient.get<Blob>(`/admin/projects/${encodeURIComponent(projectId)}/deliveries/${encodeURIComponent(submissionId)}/files/${encodeURIComponent(fileId)}`,
    { responseType: "blob" })).data;
}
