"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";

import { deleteProject } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";

export function useDeleteProject() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (projectId: string) => deleteProject(projectId),

    onSuccess: async (_data, projectId) => {
      queryClient.removeQueries({
        queryKey: projectQueryKeys.workspace(projectId),
      });

      await queryClient.invalidateQueries({
        queryKey: ["projects"],
      });
    },
  });
}
