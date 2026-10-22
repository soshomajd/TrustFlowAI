import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { AdminDisputeKind, AdminDisputes, AdminDisputeDetail } from "@/features/admin/types/admin-disputes";
export async function getAdminDisputes(projectId: string, kind: AdminDisputeKind, page: number, signal?: AbortSignal) {
  return (await apiClient.get<AdminDisputes>(`/admin/projects/${encodeURIComponent(projectId)}/disputes`, { params: { kind, page, pageSize: 20 }, signal })).data;
}
export async function getAdminDispute(projectId: string, kind: AdminDisputeKind, id: string, signal?: AbortSignal) {
  return (await apiClient.get<AdminDisputeDetail>(`/admin/projects/${encodeURIComponent(projectId)}/disputes/${kind}/${encodeURIComponent(id)}`, { signal })).data;
}
