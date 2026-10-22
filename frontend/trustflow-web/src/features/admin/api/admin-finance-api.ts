import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { FinanceEscrow, FinancePage, FinancePayout, FinanceSettlement, FinanceSection } from "@/features/admin/types/admin-finance";
export async function getAdminFinance(projectId: string, page: number, signal?: AbortSignal) {
  return (await apiClient.get<FinancePage<FinanceEscrow>>(`/admin/projects/${encodeURIComponent(projectId)}/finance`, { params: { page, pageSize: 20 }, signal })).data;
}
export async function getAdminFinanceEntries(projectId: string, escrowId: string, section: FinanceSection, page: number, signal?: AbortSignal) {
  return (await apiClient.get<FinancePage<FinancePayout | FinanceSettlement>>(`/admin/projects/${encodeURIComponent(projectId)}/finance/${encodeURIComponent(escrowId)}/${section}`,
    { params: { page, pageSize: 20 }, signal })).data;
}

export async function downloadAdminFinanceReport(projectId: string, escrowId: string) {
  return (await apiClient.get<Blob>(`/admin/projects/${encodeURIComponent(projectId)}/finance/${encodeURIComponent(escrowId)}/report.csv`,
    { responseType: "blob" })).data;
}
