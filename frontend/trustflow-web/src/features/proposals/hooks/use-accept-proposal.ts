"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { deployEscrow, getProjectEscrowParticipants, getEscrowStages } from "@/features/escrow/api/escrow-api";
import { fundStagedMilestone } from "@/features/escrow/lib/fund-staged-milestone";
import { acceptProposal } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useAcceptProposal(projectId: string, proposalId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedProposalId = proposalId.trim();

  return useMutation({
    mutationKey: proposalQueryKeys.accept(
      normalizedProjectId,
      normalizedProposalId,
    ),

    mutationFn: async () => {
      const result = await acceptProposal(normalizedProjectId, normalizedProposalId);
      if (!result.requiresFunding) return result;
      let escrow = (await getProjectEscrowParticipants(projectId)).find(p => p.freelancerId === freelancerId)?.escrow;
      if (!escrow) throw new Error("The reserved contract could not be loaded. Refresh and resume funding.");
      if (escrow.status === "PendingDeployment" || escrow.status === "Deploying")
        escrow = await deployEscrow(projectId, freelancerId);
      const stage = (await getEscrowStages(projectId, freelancerId)).find(s => s.chainIndex === 0);
      if (!stage) throw new Error("The first milestone is missing from the funding plan.");
      await fundStagedMilestone(projectId, escrow, stage);
      return result;
    },

    onSuccess: async (result) => {
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

      toast.success(result.requiresFunding
        ? "Milestone funded. Waiting for the freelancer to accept and start."
        : "Proposal accepted. Open the existing escrow controls to fund this legacy contract.");
    },

    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: proposalQueryKeys.all });
      await queryClient.invalidateQueries({ queryKey: escrowQueryKeys.participants(normalizedProjectId) });
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Proposal could not be accepted."));
    },
  });
}
