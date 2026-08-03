"use client";

import { useQuery } from "@tanstack/react-query";

import { getProjectProposals } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import type { GetProjectProposalsParams } from "@/features/proposals/types/proposal";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useProjectProposals(
  projectId: string | undefined,
  params: GetProjectProposalsParams,
) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const normalizedParams = {
    page: params.page && params.page >= 1 ? params.page : 1,

    pageSize: params.pageSize && params.pageSize >= 1 ? params.pageSize : 5,
  };

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: proposalQueryKeys.projectList(
      normalizedProjectId,
      normalizedParams,
    ),

    queryFn: () => getProjectProposals(normalizedProjectId, normalizedParams),

    enabled: isProjectIdValid,

    placeholderData: (previousData) => previousData,
  });
}
