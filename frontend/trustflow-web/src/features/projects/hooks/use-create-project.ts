"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { createProject } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type { CreateProjectRequest } from "@/features/projects/types/project";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useCreateProject() {
  const router = useRouter();
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: projectQueryKeys.create(),

    mutationFn: (request: CreateProjectRequest) => createProject(request),

    onSuccess: async (createdProject) => {
      await queryClient.invalidateQueries({
        queryKey: projectQueryKeys.client(),
      });

      toast.success(`"${createdProject.title}" was created successfully.`);

      router.replace("/dashboard/client/projects");

      router.refresh();
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Project could not be created."));
    },
  });
}
