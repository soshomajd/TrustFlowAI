"use client";

import { useQuery } from "@tanstack/react-query";

import { getProjectWorkspace } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useProjectWorkspace(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: projectQueryKeys.workspace(normalizedProjectId),

    queryFn: ({ signal }) => getProjectWorkspace(normalizedProjectId, signal),

    enabled: isProjectIdValid,

    staleTime: 15_000,

    refetchInterval: 15_000,

    refetchIntervalInBackground: false,

    refetchOnWindowFocus: true,

    retry: 1,
  });
}
