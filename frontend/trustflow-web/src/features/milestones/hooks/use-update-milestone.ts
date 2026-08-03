"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { updateMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import type { UpdateMilestoneRequest } from "@/features/milestones/types/milestone";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

type UpdateMilestoneVariables = {
  milestoneId: string;
  request: UpdateMilestoneRequest;
};

export function useUpdateMilestone(projectId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: [...milestoneQueryKeys.all, "update", normalizedProjectId],

    mutationFn: ({ milestoneId, request }: UpdateMilestoneVariables) =>
      updateMilestone(normalizedProjectId, milestoneId, request),

    onSuccess: async (updatedMilestone) => {
      queryClient.setQueryData(
        milestoneQueryKeys.detail(normalizedProjectId, updatedMilestone.id),
        updatedMilestone,
      );

      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
      ]);

      toast.success(`"${updatedMilestone.title}" was updated successfully.`);
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Milestone could not be updated."));
    },
  });
}
