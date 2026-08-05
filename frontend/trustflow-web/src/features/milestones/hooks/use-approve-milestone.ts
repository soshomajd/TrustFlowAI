"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { approveMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useApproveMilestone(projectId: string, milestoneId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedMilestoneId = milestoneId.trim();

  return useMutation({
    mutationKey: milestoneQueryKeys.approve(
      normalizedProjectId,
      normalizedMilestoneId,
    ),

    mutationFn: () =>
      approveMilestone(normalizedProjectId, normalizedMilestoneId),

    onSuccess: async (response) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.all,
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.assignedLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientDashboardSummary(),
        }),
      ]);

      toast.success(
        response.projectStatus === "Completed"
          ? "Milestone approved and project completed."
          : "Milestone approved.",
      );
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Milestone could not be approved."),
      );
    },
  });
}
