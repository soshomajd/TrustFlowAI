import { useQuery } from "@tanstack/react-query";

import { getMilestoneSubmissions } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";

export function useMilestoneSubmissions(projectId: string, milestoneId: string) {
  return useQuery({
    queryKey: milestoneQueryKeys.submissions(projectId, milestoneId),
    queryFn: () => getMilestoneSubmissions(projectId, milestoneId),
    enabled: Boolean(projectId && milestoneId),
  });
}
