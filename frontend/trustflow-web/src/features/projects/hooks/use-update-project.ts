"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";

import { updateProject } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type { CreateProjectRequest } from "@/features/projects/types/project";

type UpdateProjectVariables = {
  projectId: string;
  request: CreateProjectRequest;
};

export function useUpdateProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ projectId, request }: UpdateProjectVariables) =>
      updateProject(projectId, request),

    onSuccess: async (_updatedProject, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: ["projects"],
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(variables.projectId),
        }),
      ]);
    },
  });
}
