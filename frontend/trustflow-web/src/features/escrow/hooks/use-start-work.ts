"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { readContract, waitForTransactionReceipt, writeContract } from "wagmi/actions";
import { readContractQueryKey } from "wagmi/query";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { startWorkWithRecovery } from "@/features/escrow/lib/recoverable-escrow-actions";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

type StartWorkVariables = {
  contractAddress: string;
  chainId: number;
};

export function useStartWork(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.startWork(normalizedProjectId, freelancerId),

    mutationFn: async ({ contractAddress, chainId }: StartWorkVariables) => {
      const address = contractAddress as Address;
      return startWorkWithRecovery({
        sync: () => syncEscrow(normalizedProjectId, freelancerId),
        readStarted: () => readContract(wagmiConfig, {
          address,
          abi: escrowAbi,
          functionName: "workStarted",
          chainId,
        }),
        start: () => confirmEscrowTransaction(`${chainId}:${address.toLowerCase()}:start`, () => writeContract(wagmiConfig, {
          address,
          abi: escrowAbi,
          functionName: "startWork",
          chainId,
        }), (hash) => waitForTransactionReceipt(wagmiConfig, { hash, chainId })),
      });
    },

    onSuccess: async (_, { contractAddress, chainId }) => {
      await Promise.all([
        invalidateEscrowWorkflow(queryClient, normalizedProjectId),
        queryClient.invalidateQueries({ queryKey: readContractQueryKey({
          address: contractAddress as Address,
          abi: escrowAbi,
          functionName: "workStarted",
          chainId,
        }) }),
      ]);
      toast.success("Work start confirmed on-chain.");
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Could not start work."));
    },
  });
}
