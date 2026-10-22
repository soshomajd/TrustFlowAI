import "client-only";

import { apiClient } from "@/lib/api/api-client";
import type { AdminOverview } from "@/features/admin/types/admin-overview";

export async function getAdminOverview(signal?: AbortSignal) {
  const response = await apiClient.get<AdminOverview>("/admin/overview", { signal });
  return response.data;
}
