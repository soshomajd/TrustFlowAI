"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { readContract, waitForTransactionReceipt, writeContract } from "wagmi/actions";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { cancelEscrowWithRecovery } from "@/features/escrow/lib/recoverable-escrow-actions";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export function useCancelEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.cancel(normalizedProjectId, freelancerId),

    mutationFn: async (escrow: ProjectEscrow) => {
      if (!escrow.contractAddress) {
        throw new Error("The escrow contract has not been deployed yet.");
      }

      const address = escrow.contractAddress as Address;
      return cancelEscrowWithRecovery({
        readState: () => readContract(wagmiConfig, {
          address,
          abi: escrowAbi,
          functionName: "escrowState",
          chainId: escrow.chainId,
        }),
        cancel: () => confirmEscrowTransaction(`${escrow.chainId}:${address.toLowerCase()}:cancel`, () => writeContract(wagmiConfig, {
          address,
          abi: escrowAbi,
          functionName: "cancelBeforeWorkStarts",
          chainId: escrow.chainId,
        }), (hash) => waitForTransactionReceipt(wagmiConfig, {
          hash,
          chainId: escrow.chainId,
        })),
        sync: () => syncEscrow(normalizedProjectId, freelancerId),
      });
    },

    onSuccess: async () => {
      await invalidateEscrowWorkflow(queryClient, normalizedProjectId);

      toast.success(
        "Escrow cancelled. The freelancer has a notification, and those stages are open for hire again.",
      );
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Escrow cancellation failed."));
    },
  });
}
