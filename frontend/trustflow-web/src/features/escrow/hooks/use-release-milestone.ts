"use client";

import { useMutation } from "@tanstack/react-query";
import type { Address } from "viem";
import {
  readContract,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { releaseMilestoneWithRecovery } from "@/features/escrow/lib/recoverable-escrow-actions";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type ReleaseMilestoneVariables = {
  contractAddress: string;
  onChainIndex: number;
  chainId: number;
};

export function useReleaseMilestone(projectId: string) {
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: [
      ...escrowQueryKeys.all,
      "release-milestone",
      normalizedProjectId,
    ],

    mutationFn: async ({
      contractAddress,
      onChainIndex,
      chainId,
    }: ReleaseMilestoneVariables) => {
      const address = contractAddress as Address;
      if (!Number.isSafeInteger(onChainIndex) || onChainIndex < 0) {
        throw new Error("The milestone position is invalid. Refresh this project before approving.");
      }
      return releaseMilestoneWithRecovery({
        readReleased: async () => {
          const milestone = await readContract(wagmiConfig, {
            address,
            abi: escrowAbi,
            functionName: "getMilestone",
            args: [BigInt(onChainIndex)],
            chainId,
          });
          if (milestone[1] && await readContract(wagmiConfig, {
            address,
            abi: escrowAbi,
            functionName: "isSettledByCase",
            args: [BigInt(onChainIndex)],
            chainId,
          })) {
            throw new Error("This milestone was settled through a case. Refresh the project to see the result.");
          }
          return milestone[1];
        },
        release: () => confirmEscrowTransaction(`${chainId}:${address.toLowerCase()}:release:${onChainIndex}`, () => writeContract(wagmiConfig, {
          address,
          abi: escrowAbi,
          functionName: "releaseMilestone",
          args: [BigInt(onChainIndex)],
          chainId,
        }), (hash) => waitForTransactionReceipt(wagmiConfig, { hash, chainId })),
      });
    },
  });
}
