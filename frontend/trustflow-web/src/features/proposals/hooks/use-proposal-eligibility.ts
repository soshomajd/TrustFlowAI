"use client";

import { useQuery } from "@tanstack/react-query";
import { getProposalEligibility } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { useAuthStore } from "@/stores/auth-store";

export function useProposalEligibility(projectId: string) {
  const userId = useAuthStore((state) => state.user?.id);
  return useQuery({
    queryKey: proposalQueryKeys.eligibility(projectId, userId ?? ""),
    queryFn: ({ signal }) => getProposalEligibility(projectId, signal),
    enabled: Boolean(userId),
    staleTime: 0,
    refetchOnMount: "always",
    retry: 1,
  });
}
