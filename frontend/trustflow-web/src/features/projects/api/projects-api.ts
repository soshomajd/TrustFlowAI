import "client-only";

import type {
  AssignedProject,
  ClientProject,
  CreateProjectRequest,
  CreatedProjectResponse,
  GetAssignedProjectsParams,
  GetClientProjectsParams,
} from "@/features/projects/types/project";
import type { ClientDashboardSummary } from "@/features/projects/types/client-dashboard";
import type { ProjectWorkspace } from "@/features/projects/types/project-workspace";
import { apiClient } from "@/lib/api/api-client";
import type { PagedResponse } from "@/types/pagination";
import type { FreelancerDashboardSummary } from "@/features/projects/types/freelancer-dashboard";

export async function getClientProjects(
  params: GetClientProjectsParams,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<PagedResponse<ClientProject>>(
    "/projects/mine",
    {
      params,
      signal,
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

export async function updateProject(
  projectId: string,
  request: CreateProjectRequest,
) {
  const response = await apiClient.put<CreatedProjectResponse>(
    `/projects/${projectId}`,
    request,
  );

  return response.data;
}

export async function deleteProject(projectId: string): Promise<void> {
  await apiClient.delete(`/projects/${projectId}`);
}

export async function getProjectWorkspace(
  projectId: string,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<ProjectWorkspace>(
    `/projects/${projectId}/workspace`,
    {
      signal,
    },
  );

  return response.data;
}

export async function getAssignedProjects(
  params: GetAssignedProjectsParams,
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

export async function getFreelancerDashboardSummary(signal?: AbortSignal) {
  const response = await apiClient.get<FreelancerDashboardSummary>(
    "/projects/freelancer-dashboard-summary",
    {
      signal,
    },
  );

  return response.data;
}
