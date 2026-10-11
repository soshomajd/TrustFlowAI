"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { waitForTransactionReceipt, writeContract } from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

type StartWorkVariables = {
  contractAddress: string;
  chainId: number;
};

export function useStartWork(projectId: string, freelancerId: string) {
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.startWork(normalizedProjectId, freelancerId),

    mutationFn: async ({ contractAddress, chainId }: StartWorkVariables) => {
      const hash = await writeContract(wagmiConfig, {
        address: contractAddress as Address,
        abi: escrowAbi,
        functionName: "startWork",
        chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, { hash, chainId });
    },

    onSuccess: () => {
      toast.success("Work started on-chain. You can now start milestones.");
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Could not start work."));
    },
  });
}
