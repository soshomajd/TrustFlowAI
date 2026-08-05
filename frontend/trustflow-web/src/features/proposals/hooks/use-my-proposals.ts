"use client";

import { useQuery } from "@tanstack/react-query";

import { getMyProposals } from "@/features/proposals/api/proposals-api";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import type { GetMyProposalsParams } from "@/features/proposals/types/proposal";

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 10;

export function useMyProposals(params: GetMyProposalsParams = {}) {
  const normalizedParams = {
    page: normalizePositiveInteger(params.page, DEFAULT_PAGE),

    pageSize: normalizePositiveInteger(params.pageSize, DEFAULT_PAGE_SIZE),
  };

  return useQuery({
    queryKey: proposalQueryKeys.myList(normalizedParams),

    queryFn: ({ signal }) => getMyProposals(normalizedParams, signal),

    placeholderData: (previousData) => previousData,

    staleTime: 30_000,
  });
}

function normalizePositiveInteger(value: number | undefined, fallback: number) {
  if (value === undefined || !Number.isFinite(value)) {
    return fallback;
  }

  return Math.max(1, Math.trunc(value));
}
