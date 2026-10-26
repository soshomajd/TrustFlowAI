"use client";

import { useQuery } from "@tanstack/react-query";
import { caseWindowReadiness, type ExpectedCase } from "@/features/escrow/lib/recoverable-case-actions";
import { readCaseChainState } from "@/features/escrow/lib/read-case-chain-state";

export function useCaseWindow(address: string | null, chainId: number, index: number, expectedCase: ExpectedCase) {
  const enabled = !!address && Number.isSafeInteger(index) && index >= 0;
  const query = useQuery({
    queryKey: ["escrow-case-window", chainId, address?.toLowerCase(), index, expectedCase.kind, expectedCase.openedAt],
    enabled,
    queryFn: () => readCaseChainState(address!, chainId, BigInt(index)),
    staleTime: 0,
    refetchInterval: 15_000,
    refetchOnWindowFocus: true,
    retry: 1,
  });
  const readiness = caseWindowReadiness(enabled && !query.isError ? query.data ?? null : null, expectedCase, index);
  return { ...query, ...readiness };
}
