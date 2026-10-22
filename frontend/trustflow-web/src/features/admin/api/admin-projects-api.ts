import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { AdminProjectQuery, AdminProjectsPage } from "@/features/admin/types/admin-project";

export async function getAdminProjects(params: AdminProjectQuery, signal?: AbortSignal) {
  const response = await apiClient.get<AdminProjectsPage>("/admin/projects", { params, signal });
  return response.data;
}
