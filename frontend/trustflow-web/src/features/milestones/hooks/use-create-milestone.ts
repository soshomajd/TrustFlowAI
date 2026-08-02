"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { createMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import type { CreateMilestoneRequest } from "@/features/milestones/types/milestone";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useCreateMilestone(projectId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: milestoneQueryKeys.create(normalizedProjectId),

    mutationFn: (request: CreateMilestoneRequest) =>
      createMilestone(normalizedProjectId, request),

    onSuccess: async (createdMilestone) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
      ]);

      toast.success(`"${createdMilestone.title}" was created successfully.`);
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Milestone could not be created."));
    },
  });
}
