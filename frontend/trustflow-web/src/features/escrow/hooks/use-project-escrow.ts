"use client";

import { useQuery } from "@tanstack/react-query";

import { getProjectEscrow } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useProjectEscrow(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: escrowQueryKeys.detail(normalizedProjectId),

    queryFn: ({ signal }) =>
      getProjectEscrow(normalizedProjectId, signal),

    enabled: isProjectIdValid,

    staleTime: 15_000,

    retry: 1,
  });
}
