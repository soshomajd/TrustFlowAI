"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { createProposal } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import type { CreateProposalRequest } from "@/features/proposals/types/proposal";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useCreateProposal(projectId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: proposalQueryKeys.create(normalizedProjectId),

    mutationFn: (request: CreateProposalRequest) =>
      createProposal(normalizedProjectId, request),

    onSuccess: async (createdProposal) => {
      await queryClient.invalidateQueries({
        queryKey: proposalQueryKeys.projectLists(),
      });

      toast.success(
        `Proposal submitted with status ${createdProposal.status}.`,
      );
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Proposal could not be submitted."),
      );
    },
  });
}
