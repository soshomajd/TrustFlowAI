"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getClientProjects } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type { GetClientProjectsParams } from "@/features/projects/types/project";

export function useClientProjects(params: GetClientProjectsParams = {}) {
  const normalizedParams = {
    page: params.page ?? 1,
    pageSize: params.pageSize ?? 10,
    status: params.status,
  } satisfies GetClientProjectsParams;

  return useQuery({
    queryKey: projectQueryKeys.clientList(normalizedParams),

    queryFn: () => getClientProjects(normalizedParams),

    placeholderData: keepPreviousData,
  });
}
