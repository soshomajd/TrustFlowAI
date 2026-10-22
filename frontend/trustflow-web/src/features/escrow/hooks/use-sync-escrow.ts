"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { readContractQueryKey } from "wagmi/query";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useSyncEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.sync(normalizedProjectId, freelancerId),

    mutationFn: () => syncEscrow(normalizedProjectId, freelancerId),

    onSuccess: async (escrow) => {
      await invalidateEscrowWorkflow(queryClient, normalizedProjectId);
      if (escrow.contractAddress) {
        await queryClient.invalidateQueries({ queryKey: readContractQueryKey({
          address: escrow.contractAddress as Address,
          abi: escrowAbi,
          functionName: "workStarted",
          chainId: escrow.chainId,
        }) });
      }

      if (escrow.status === "Funded") {
        toast.success("Escrow funding confirmed on-chain.");
      } else if (escrow.status === "Cancelled") {
        toast.success("Cancellation and refund confirmed on-chain.");
      } else {
        toast.info("Escrow status refreshed.");
      }
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Escrow status could not be refreshed."),
      );
    },
  });
}
