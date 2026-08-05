"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { acceptProposal } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useAcceptProposal(projectId: string, proposalId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedProposalId = proposalId.trim();

  return useMutation({
    mutationKey: proposalQueryKeys.accept(
      normalizedProjectId,
      normalizedProposalId,
    ),

    mutationFn: () => acceptProposal(normalizedProjectId, normalizedProposalId),

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.projectLists(),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.assignedLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.myLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientDashboardSummary(),
        }),
      ]);

      toast.success("Proposal accepted and freelancer assigned.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Proposal could not be accepted."));
    },
  });
}
