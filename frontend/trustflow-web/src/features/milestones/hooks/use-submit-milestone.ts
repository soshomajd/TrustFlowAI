"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { submitMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useSubmitMilestone(projectId: string, milestoneId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedMilestoneId = milestoneId.trim();

  return useMutation({
    mutationKey: milestoneQueryKeys.submit(
      normalizedProjectId,
      normalizedMilestoneId,
    ),

    mutationFn: () =>
      submitMilestone(normalizedProjectId, normalizedMilestoneId),

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.assignedLists(),
        }),
      ]);

      toast.success("Milestone submitted for client review.");
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Milestone could not be submitted."),
      );
    },
  });
}
