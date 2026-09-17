"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { rejectMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useRejectMilestone(projectId: string, milestoneId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  const normalizedMilestoneId = milestoneId.trim();

  return useMutation({
    mutationKey: milestoneQueryKeys.reject(
      normalizedProjectId,
      normalizedMilestoneId,
    ),

    mutationFn: (reason: string) =>
      rejectMilestone(normalizedProjectId, normalizedMilestoneId, reason),

    onSuccess: async () => {
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
          queryKey: milestoneQueryKeys.submissions(normalizedProjectId, normalizedMilestoneId),
        }),
      ]);

      toast.success("Changes requested from the freelancer.");
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "The change request could not be sent."),
      );
    },
  });
}
