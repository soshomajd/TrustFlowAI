"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { rejectProposal } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useRejectProposal(projectId: string, proposalId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedProposalId = proposalId.trim();

  return useMutation({
    mutationKey: proposalQueryKeys.reject(
      normalizedProjectId,
      normalizedProposalId,
    ),

    mutationFn: () => rejectProposal(normalizedProjectId, normalizedProposalId),

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.projectLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.myLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
      ]);

      toast.success("Proposal rejected.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Proposal could not be rejected."));
    },
  });
}
