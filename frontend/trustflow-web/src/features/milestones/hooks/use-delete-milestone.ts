"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { deleteMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useDeleteMilestone(projectId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: [...milestoneQueryKeys.all, "delete", normalizedProjectId],

    mutationFn: (milestoneId: string) =>
      deleteMilestone(normalizedProjectId, milestoneId),

    onSuccess: async (_, deletedMilestoneId) => {
      queryClient.removeQueries({
        queryKey: milestoneQueryKeys.detail(
          normalizedProjectId,
          deletedMilestoneId,
        ),
        exact: true,
      });

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
      ]);

      toast.success("Milestone was deleted successfully.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Milestone could not be deleted."));
    },
  });
}
