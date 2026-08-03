import "client-only";

import type {
  ProjectMilestone,
  CreateMilestoneRequest,
  UpdateMilestoneRequest,
} from "@/features/milestones/types/milestone";
import { apiClient } from "@/lib/api/api-client";

export async function getProjectMilestones(projectId: string) {
  const response = await apiClient.get<ProjectMilestone[]>(
    `/projects/${projectId}/milestones`,
  );

  return response.data;
}

export async function createMilestone(
  projectId: string,
  request: CreateMilestoneRequest,
) {
  const response = await apiClient.post<ProjectMilestone>(
    `/projects/${projectId}/milestones`,
    request,
  );

  return response.data;
}

export async function updateMilestone(
  projectId: string,
  milestoneId: string,
  request: UpdateMilestoneRequest,
) {
  const response = await apiClient.put<ProjectMilestone>(
    `/projects/${projectId}/milestones/${milestoneId}`,
    request,
  );
  return response.data;
}
export async function deleteMilestone(projectId: string, milestoneId: string) {
  await apiClient.delete(`/projects/${projectId}/milestones/${milestoneId}`);
}
