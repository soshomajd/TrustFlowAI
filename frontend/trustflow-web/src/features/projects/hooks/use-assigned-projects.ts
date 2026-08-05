"use client";

import { useQuery } from "@tanstack/react-query";

import { getAssignedProjects } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type { GetAssignedProjectsParams } from "@/features/projects/types/project";

const DEFAULT_PAGE = 1;
const DEFAULT_PAGE_SIZE = 10;

export function useAssignedProjects(params: GetAssignedProjectsParams = {}) {
  const normalizedParams = {
    page: normalizePositiveInteger(params.page, DEFAULT_PAGE),

    pageSize: normalizePositiveInteger(params.pageSize, DEFAULT_PAGE_SIZE),
  };

  return useQuery({
    queryKey: projectQueryKeys.assignedList(normalizedParams),

    queryFn: ({ signal }) => getAssignedProjects(normalizedParams, signal),

    placeholderData: (previousData) => previousData,

    staleTime: 30_000,

    retry: 1,
  });
}

function normalizePositiveInteger(value: number | undefined, fallback: number) {
  if (value === undefined || !Number.isFinite(value)) {
    return fallback;
  }

  return Math.max(1, Math.trunc(value));
}
