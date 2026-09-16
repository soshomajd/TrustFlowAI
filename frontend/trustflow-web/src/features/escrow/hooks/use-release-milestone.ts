"use client";

import { useMutation } from "@tanstack/react-query";
import type { Address } from "viem";
import { waitForTransactionReceipt, writeContract } from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type ReleaseMilestoneVariables = {
  contractAddress: string;
  onChainIndex: number;
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
    }: ReleaseMilestoneVariables) => {
      const hash = await writeContract(wagmiConfig, {
        address: contractAddress as Address,
        abi: escrowAbi,
        functionName: "releaseMilestone",
        args: [BigInt(onChainIndex)],
      });

      await waitForTransactionReceipt(wagmiConfig, { hash });
    },
  });
}
