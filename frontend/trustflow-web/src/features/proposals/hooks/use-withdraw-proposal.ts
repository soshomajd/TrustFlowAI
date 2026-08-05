"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { withdrawProposal } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useWithdrawProposal(proposalId: string) {
  const queryClient = useQueryClient();

  const normalizedProposalId = proposalId.trim();

  return useMutation({
    mutationKey: proposalQueryKeys.withdraw(normalizedProposalId),

    mutationFn: () => withdrawProposal(normalizedProposalId),

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.myLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.projectLists(),
        }),
      ]);

      toast.success("Proposal withdrawn.");
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Proposal could not be withdrawn."),
      );
    },
  });
}
