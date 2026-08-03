import "client-only";

import type { PagedResponse } from "@/types/pagination";
import type {
  ClientProjectProposal,
  CreatedProposal,
  GetProjectProposalsParams,
  CreateProposalRequest,
} from "@/features/proposals/types/proposal";
import { apiClient } from "@/lib/api/api-client";

export async function getProjectProposals(
  projectId: string,
  params: GetProjectProposalsParams,
) {
  const response = await apiClient.get<PagedResponse<ClientProjectProposal>>(
    `/projects/${projectId}/proposals`,
    {
      params,
    },
  );

  return response.data;
}

export async function createProposal(
  projectId: string,
  request: CreateProposalRequest,
) {
  const response = await apiClient.post<CreatedProposal>(
    `/projects/${projectId}/proposals`,
    request,
  );

  return response.data;
}
