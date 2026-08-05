import "client-only";

import type { PagedResponse } from "@/types/pagination";
import type {
  AcceptProposalResponse,
  ClientProjectProposal,
  CreatedProposal,
  CreateProposalRequest,
  GetMyProposalsParams,
  GetProjectProposalsParams,
  MyProposal,
  ProposalStatusResponse,
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
export async function getMyProposals(
  params: Required<GetMyProposalsParams>,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<PagedResponse<MyProposal>>(
    "/proposals/mine",
    {
      params,
      signal,
    },
  );

  return response.data;
}

export async function withdrawProposal(proposalId: string) {
  const response = await apiClient.patch<ProposalStatusResponse>(
    `/proposals/${proposalId}/withdraw`,
  );

  return response.data;
}
export async function acceptProposal(projectId: string, proposalId: string) {
  const response = await apiClient.patch<AcceptProposalResponse>(
    `/projects/${projectId}/proposals/${proposalId}/accept`,
  );

  return response.data;
}

export async function rejectProposal(projectId: string, proposalId: string) {
  const response = await apiClient.patch<ProposalStatusResponse>(
    `/projects/${projectId}/proposals/${proposalId}/reject`,
  );

  return response.data;
}
