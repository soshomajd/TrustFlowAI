"use client";

import { useQuery } from "@tanstack/react-query";
import { getProjectMilestones } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useProjectMilestones(projectId: string | undefined) {
  const normalizedProjectId = projectId?.trim() ?? "";

  const isProjectIdValid = isValidGuid(normalizedProjectId);

  return useQuery({
    queryKey: milestoneQueryKeys.projectList(normalizedProjectId),

    queryFn: () => getProjectMilestones(normalizedProjectId),

    enabled: isProjectIdValid,
  });
}
