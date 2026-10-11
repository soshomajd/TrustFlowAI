"use client";

import { useQuery } from "@tanstack/react-query";

import { getProjectEscrowParticipants } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useProjectEscrows(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: escrowQueryKeys.participants(normalizedProjectId),

    queryFn: ({ signal }) =>
      getProjectEscrowParticipants(normalizedProjectId, signal),

    enabled: isProjectIdValid,

    staleTime: 15_000,

    retry: 1,
  });
}
