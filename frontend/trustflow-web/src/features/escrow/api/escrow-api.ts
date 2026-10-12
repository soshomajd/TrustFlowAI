import "client-only";

import type {
  CreateEscrowRequest,
  EscrowCase,
  ProjectEscrow,
  ProjectEscrowParticipant,
} from "@/features/escrow/types/escrow";
import { apiClient } from "@/lib/api/api-client";

// Every freelancer assigned to the project, each with their own escrow once
// the client has created it. A freelancer only ever gets their own row.
export async function getProjectEscrowParticipants(
  projectId: string,
  signal?: AbortSignal,
): Promise<ProjectEscrowParticipant[]> {
  const response = await apiClient.get<ProjectEscrowParticipant[]>(
    `/projects/${projectId}/escrows`,
    { signal },
  );

  return response.data;
}

// The cases on this project's escrows that the caller is a side of.
export async function getProjectEscrowCases(
  projectId: string,
  signal?: AbortSignal,
): Promise<EscrowCase[]> {
  const response = await apiClient.get<EscrowCase[]>(
    `/projects/${projectId}/escrow-cases`,
    { signal },
  );

  return response.data;
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

export async function deployEscrow(projectId: string, freelancerId: string) {
  const response = await apiClient.post<ProjectEscrow>(
    `/projects/${projectId}/escrow/deploy`,
    undefined,
    { params: { freelancerId } },
  );

  return response.data;
}

export async function syncEscrow(projectId: string, freelancerId: string) {
  const response = await apiClient.post<ProjectEscrow>(
    `/projects/${projectId}/escrow/sync`,
    undefined,
    { params: { freelancerId } },
  );

  return response.data;
}
