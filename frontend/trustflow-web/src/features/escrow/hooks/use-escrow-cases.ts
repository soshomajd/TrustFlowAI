"use client";

import { useQuery } from "@tanstack/react-query";

import { getProjectEscrowCases } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

// Every case on the project's escrows that the viewer is a side of. A case
// changes only when someone signs on the contract, so a short poll is enough
// to show the other side's move.
export function useEscrowCases(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  return useQuery({
    queryKey: escrowQueryKeys.cases(normalizedProjectId),

    queryFn: ({ signal }) =>
      getProjectEscrowCases(normalizedProjectId, signal),

    enabled: isValidGuid(normalizedProjectId),

    staleTime: 15_000,

    refetchInterval: 30_000,

    retry: 1,
  });
}
