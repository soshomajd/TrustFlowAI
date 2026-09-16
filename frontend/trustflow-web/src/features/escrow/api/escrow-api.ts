import "client-only";

import { isAxiosError } from "axios";

import type {
  CreateEscrowRequest,
  ProjectEscrow,
} from "@/features/escrow/types/escrow";
import { apiClient } from "@/lib/api/api-client";

export async function getProjectEscrow(
  projectId: string,
  signal?: AbortSignal,
): Promise<ProjectEscrow | null> {
  try {
    const response = await apiClient.get<ProjectEscrow>(
      `/projects/${projectId}/escrow`,
      { signal },
    );

    return response.data;
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 404) {
      return null;
    }

    throw error;
  }
}

export async function createEscrow(
  projectId: string,
  request: CreateEscrowRequest,
) {
  const response = await apiClient.post<ProjectEscrow>(
    `/projects/${projectId}/escrow`,
    request,
  );

  return response.data;
}

export async function deployEscrow(projectId: string) {
  const response = await apiClient.post<ProjectEscrow>(
    `/projects/${projectId}/escrow/deploy`,
  );

  return response.data;
}

export async function syncEscrow(projectId: string) {
  const response = await apiClient.post<ProjectEscrow>(
    `/projects/${projectId}/escrow/sync`,
  );

  return response.data;
}
