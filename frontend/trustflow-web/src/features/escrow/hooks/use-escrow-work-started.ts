"use client";

import type { Address } from "viem";
import { useReadContract } from "wagmi";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";

export function useEscrowWorkStarted(escrow: ProjectEscrow | null) {
  return useReadContract({
    address: escrow?.contractAddress as Address | undefined,
    abi: escrowAbi,
    functionName: "workStarted",
    chainId: escrow?.chainId,
    query: {
      enabled: Boolean(escrow?.contractAddress),
      refetchInterval: 5_000,
    },
  });
}
