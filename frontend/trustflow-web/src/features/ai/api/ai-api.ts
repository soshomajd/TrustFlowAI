import "client-only";

import axios from "axios";

import type {
  AiStatus,
  MilestoneAiReview,
  ProfileCoaching,
  ProjectDraft,
  ProjectDraftRequest,
  ResumeDraft,
  ResumeDraftRequest,
} from "@/features/ai/types/ai";
import type { Dispute } from "@/features/ai/types/dispute";
import { apiClient } from "@/lib/api/api-client";

// A model call can take much longer than an ordinary request.
const AI_TIMEOUT = { timeout: 120_000 } as const;

export async function getAiStatus() {
  const response = await apiClient.get<AiStatus>("/ai/status");

  return response.data;
}

export async function draftProject(request: ProjectDraftRequest) {
  const response = await apiClient.post<ProjectDraft>(
    "/ai/project-draft",
    request,
    AI_TIMEOUT,
  );

  return response.data;
}

export async function coachProfile() {
  const response = await apiClient.post<ProfileCoaching>(
    "/ai/freelancer/profile-coach",
    undefined,
    AI_TIMEOUT,
  );

  return response.data;
}

export async function draftResume(request: ResumeDraftRequest) {
  const response = await apiClient.post<ResumeDraft>(
    "/ai/freelancer/resume-draft",
    request,
    AI_TIMEOUT,
  );

  return response.data;
}

// ---- delivery review --------------------------------------------------

// A delivery that has not been reviewed yet is a normal state, not an error.
export async function getMilestoneAiReview(
  projectId: string,
  milestoneId: string,
) {
  try {
    const response = await apiClient.get<MilestoneAiReview>(
      `/projects/${projectId}/milestones/${milestoneId}/ai-review`,
    );

    return response.data;
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) {
      return null;
    }

    throw error;
  }
}

export async function createMilestoneAiReview(
  projectId: string,
  milestoneId: string,
) {
  const response = await apiClient.post<MilestoneAiReview>(
    `/projects/${projectId}/milestones/${milestoneId}/ai-review`,
    undefined,
    AI_TIMEOUT,
  );

  return response.data;
}

// ---- disputes ---------------------------------------------------------

export async function listMilestoneDisputes(
  projectId: string,
  milestoneId: string,
) {
  const response = await apiClient.get<Dispute[]>(
    `/projects/${projectId}/milestones/${milestoneId}/disputes`,
  );

  return response.data;
}

export async function openDispute(
  projectId: string,
  milestoneId: string,
  statement: string,
) {
  const response = await apiClient.post<Dispute>(
    `/projects/${projectId}/milestones/${milestoneId}/disputes`,
    { statement },
  );

  return response.data;
}

export async function respondToDispute(
  disputeId: string,
  statement: string,
) {
  const response = await apiClient.post<Dispute>(
    `/disputes/${disputeId}/response`,
    { statement },
  );

  return response.data;
}

export async function requestDisputeRuling(disputeId: string) {
  const response = await apiClient.post<Dispute>(
    `/disputes/${disputeId}/ruling`,
    undefined,
    AI_TIMEOUT,
  );

  return response.data;
}
