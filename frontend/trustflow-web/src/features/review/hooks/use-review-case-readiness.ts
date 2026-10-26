"use client";

import { useQuery } from "@tanstack/react-query";
import { isAddress } from "viem";
import { getPublicClient } from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { settlementReadiness } from "@/features/review/lib/settlement-readiness";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { configuredChain, wagmiConfig } from "@/lib/wallet/wagmi-config";

export function useReviewCaseReadiness(detail: ReviewCaseDetail) {
  const expected = {
    status: detail.case.status,
    kind: detail.case.kind,
    openedAt: detail.case.openedAt,
    index: detail.onChainMilestoneIndex,
  };
  const address = detail.contractAddress;
  const enabled = detail.case.status === "Open" && !!address && isAddress(address) &&
    detail.chainId === configuredChain.id && Number.isSafeInteger(expected.index) && expected.index >= 0;
  const query = useQuery({
    queryKey: ["review-chain-readiness", detail.chainId, address?.toLowerCase(), detail.case.id,
      expected.status, expected.kind, expected.openedAt, expected.index],
    enabled,
    queryFn: async () => {
      if (!address || !isAddress(address)) throw new Error("The escrow contract address is invalid.");
      const client = getPublicClient(wagmiConfig, { chainId: detail.chainId });
      if (!client || await client.getChainId() !== detail.chainId) {
        throw new Error("The RPC network does not match this escrow.");
      }
      const block = await client.getBlock({ blockTag: "latest" });
      if (block.number === null || !block.hash || block.timestamp <= 0n) {
        throw new Error("A confirmed chain block could not be read.");
      }
      // Pin every contract read to the block that supplied the clock. Mixed
      // latest reads could otherwise describe different cases across a reorg.
      const contract = { address, abi: escrowAbi, blockNumber: block.number } as const;
      const [stage, settled, open, nextIndex] = await Promise.all([
        client.readContract({ ...contract, functionName: "getMilestone", args: [BigInt(expected.index)] }),
        client.readContract({ ...contract, functionName: "isSettledByCase", args: [BigInt(expected.index)] }),
        client.readContract({ ...contract, functionName: "getOpenCase" }),
        client.readContract({ ...contract, functionName: "nextMilestoneIndex" }),
      ]);
      const canonical = await client.getBlock({ blockNumber: block.number });
      if (canonical.hash !== block.hash) throw new Error("The chain changed during the eligibility read. Retry the contract read.");
      return settlementReadiness(expected, {
        released: stage[1], settled, kind: open[0], openedAt: open[1],
        nextIndex, timestamp: block.timestamp,
      });
    },
    staleTime: 0,
    refetchInterval: 5_000,
    retry: 1,
  });
  const status = !enabled || query.isError || !query.data ? "unavailable" : query.data;
  return { ...query, status, ready: status === "ready" };
}
