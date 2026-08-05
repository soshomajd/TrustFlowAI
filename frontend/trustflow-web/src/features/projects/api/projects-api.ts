import "client-only";

import type {
  ClientProject,
  GetClientProjectsParams,
  CreateProjectRequest,
  CreatedProjectResponse,
  GetAssignedProjectsParams,
  AssignedProject,
} from "@/features/projects/types/project";
import { apiClient } from "@/lib/api/api-client";
import type { PagedResponse } from "@/types/pagination";
import type { ClientDashboardSummary } from "@/features/projects/types/client-dashboard";
import type { ProjectWorkspace } from "@/features/projects/types/project-workspace";

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
  const response = await apiClient.get<ClientDashboardSummary>(
    "/projects/dashboard-summary",
  );

  return response.data;
}

export async function createProject(request: CreateProjectRequest) {
  const response = await apiClient.post<CreatedProjectResponse>(
    "/projects",
    request,
  );

  return response.data;
}

export async function getProjectWorkspace(projectId: string) {
  const response = await apiClient.get<ProjectWorkspace>(
    `/projects/${projectId}/workspace`,
  );

  return response.data;
}

export async function getAssignedProjects(
  params: Required<GetAssignedProjectsParams>,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<PagedResponse<AssignedProject>>(
    "/projects/assigned-to-me",
    {
      params,
      signal,
    },
  );

  return response.data;
}
