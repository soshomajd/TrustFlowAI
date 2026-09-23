"use client";

import { useMutation } from "@tanstack/react-query";
import type { Address } from "viem";
import {
  readContract,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
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
      const milestone = await readContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "getMilestone",
        args: [BigInt(onChainIndex)],
        chainId,
      });

      if (milestone[1]) {
        return { alreadyReleased: true };
      }

      const hash = await writeContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "releaseMilestone",
        args: [BigInt(onChainIndex)],
        chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, { hash, chainId });
      return { alreadyReleased: false };
    },
  });
}
