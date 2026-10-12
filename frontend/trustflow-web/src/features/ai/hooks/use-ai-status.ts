"use client";

import { useQuery } from "@tanstack/react-query";

import { getAiStatus } from "@/features/ai/api/ai-api";
import { aiQueryKeys } from "@/features/ai/queries/ai-query-keys";

// Whether AI is switched on, and whether its answers are only a demo. Every
// AI screen reads this, so it is cached for a while.
export function useAiStatus() {
  return useQuery({
    queryKey: aiQueryKeys.status(),
    queryFn: getAiStatus,
    staleTime: 5 * 60_000,
  });
}
