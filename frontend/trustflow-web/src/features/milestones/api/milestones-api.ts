import "client-only";

import type {
  ProjectMilestone,
  CreateMilestoneRequest,
  UpdateMilestoneRequest,
  MilestoneStatusResponse,
  ApproveMilestoneResponse,
  MilestoneSubmission,
  SubmitMilestoneInput,
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

export async function startMilestone(projectId: string, milestoneId: string) {
  const response = await apiClient.patch<MilestoneStatusResponse>(
    `/projects/${projectId}/milestones/${milestoneId}/start`,
  );

  return response.data;
}

export async function submitMilestone(projectId: string, milestoneId: string, input: SubmitMilestoneInput) {
  const form = new FormData();
  form.append("note", input.note);
  if (input.previewLink.trim()) form.append("previewLink", input.previewLink.trim());
  if (input.sourceFilesLink.trim()) form.append("sourceFilesLink", input.sourceFilesLink.trim());
  input.files.forEach((file) => form.append("files", file));

  const response = await apiClient.post<MilestoneSubmission>(
    `/projects/${projectId}/milestones/${milestoneId}/submissions`,
    form,
  );

  return response.data;
}

export async function getMilestoneSubmissions(projectId: string, milestoneId: string) {
  const response = await apiClient.get<MilestoneSubmission[]>(
    `/projects/${projectId}/milestones/${milestoneId}/submissions`,
  );
  return response.data;
}

export async function downloadMilestoneSubmissionFile(
  projectId: string,
  milestoneId: string,
  submissionId: string,
  attachmentId: string,
) {
  const response = await apiClient.get<Blob>(
    `/projects/${projectId}/milestones/${milestoneId}/submissions/${submissionId}/files/${attachmentId}`,
    { responseType: "blob" },
  );
  return response.data;
}

export async function approveMilestone(projectId: string, milestoneId: string) {
  const response = await apiClient.patch<ApproveMilestoneResponse>(
    `/projects/${projectId}/milestones/${milestoneId}/approve`,
  );

  return response.data;
}

export async function rejectMilestone(projectId: string, milestoneId: string, reason: string) {
  const response = await apiClient.patch<MilestoneStatusResponse>(
    `/projects/${projectId}/milestones/${milestoneId}/request-changes`,
    { reason },
  );

  return response.data;
}
