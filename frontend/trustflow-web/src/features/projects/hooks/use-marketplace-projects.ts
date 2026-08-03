"use client";

import { useQuery } from "@tanstack/react-query";

import { getMarketplaceProjects } from "@/features/projects/api/marketplace-projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type {
  GetMarketplaceProjectsParams,
  NormalizedMarketplaceProjectsParams,
} from "@/features/projects/types/marketplace-project";

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 20;

export function useMarketplaceProjects(
  params: GetMarketplaceProjectsParams = {},
) {
  const normalizedParams = normalizeMarketplaceParams(params);

  return useQuery({
    queryKey: projectQueryKeys.marketplaceList(normalizedParams),

    queryFn: ({ signal }) => getMarketplaceProjects(normalizedParams, signal),

    placeholderData: (previousData) => previousData,

    staleTime: 30_000,
  });
}

function normalizeMarketplaceParams(
  params: GetMarketplaceProjectsParams,
): NormalizedMarketplaceProjectsParams {
  const search = params.search?.trim();

  return {
    page: normalizeInteger(params.page, DEFAULT_PAGE, 1, 500),

    pageSize: normalizeInteger(params.pageSize, DEFAULT_PAGE_SIZE, 1, 100),

    search: search && search.length > 0 ? search : undefined,

    minBudget: normalizeOptionalBudget(params.minBudget),

    maxBudget: normalizeOptionalBudget(params.maxBudget),

    deadlineBefore: params.deadlineBefore,

    sortBy: params.sortBy ?? "Newest",
  };
}

function normalizeInteger(
  value: number | undefined,
  fallback: number,
  minimum: number,
  maximum: number,
) {
  if (value === undefined || !Number.isFinite(value)) {
    return fallback;
  }

  return Math.min(maximum, Math.max(minimum, Math.trunc(value)));
}

function normalizeOptionalBudget(value: number | undefined) {
  if (value === undefined || !Number.isFinite(value) || value < 0) {
    return undefined;
  }

  return value;
}
