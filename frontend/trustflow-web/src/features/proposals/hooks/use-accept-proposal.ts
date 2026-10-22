"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
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
        queryClient.invalidateQueries({ queryKey: proposalQueryKeys.eligibilities() }),
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

        // Acceptance assigns the milestones, which is what the escrow
        // participants and the milestone list are built from.
        queryClient.invalidateQueries({
          queryKey: escrowQueryKeys.participants(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),
      ]);

      toast.success("Proposal accepted and freelancer assigned.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Proposal could not be accepted."));
    },
  });
}
