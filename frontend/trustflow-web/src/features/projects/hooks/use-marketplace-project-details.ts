"use client";

import { useQuery } from "@tanstack/react-query";

import { getMarketplaceProjectDetails } from "@/features/projects/api/marketplace-project-details-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useMarketplaceProjectDetails(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: projectQueryKeys.marketplaceDetail(normalizedProjectId),

    queryFn: ({ signal }) =>
      getMarketplaceProjectDetails(normalizedProjectId, signal),

    enabled: isProjectIdValid,

    staleTime: 30_000,

    retry: 1,
  });
}
