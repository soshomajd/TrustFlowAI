import "client-only";

import type {
  ClientProject,
  GetClientProjectsParams,
} from "@/features/projects/types/project";
import { apiClient } from "@/lib/api/api-client";
import type { PagedResponse } from "@/types/pagination";
import type { ClientDashboardSummary } from "@/features/projects/types/client-dashboard";

export async function getClientProjects(params: GetClientProjectsParams = {}) {
  const response = await apiClient.get<PagedResponse<ClientProject>>(
    "/projects/mine",
    {
      params: {
        page: params.page ?? 1,
        pageSize: params.pageSize ?? 10,
        status: params.status,
      },
    },
  );

  return response.data;
}

export async function getClientDashboardSummary() {
  await new Promise((resolve) => {
    setTimeout(resolve, 4000);
  });
  const response = await apiClient.get<ClientDashboardSummary>(
    "/projects/dashboard-summary",
  );

  return response.data;
}
