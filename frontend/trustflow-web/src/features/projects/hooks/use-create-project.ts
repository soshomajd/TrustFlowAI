"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { createMilestone } from "@/features/milestones/api/milestones-api";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import type { CreateMilestoneRequest } from "@/features/milestones/types/milestone";
import { createProject } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import type { CreateProjectRequest } from "@/features/projects/types/project";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

type CreateProjectInput = {
  project: CreateProjectRequest;

  // An optional plan, such as one drafted by AI, created right after the
  // project in order.
  milestones?: CreateMilestoneRequest[];
};

export function useCreateProject() {
  const router = useRouter();
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: projectQueryKeys.create(),

    mutationFn: async ({ project, milestones = [] }: CreateProjectInput) => {
      const createdProject = await createProject(project);

      // The milestones go in one at a time because each is checked against
      // what the earlier ones already used of the budget. If one is
      // refused, the project and the earlier milestones stay, and the rest
      // can be added by hand.
      let createdMilestones = 0;
      let failure: string | null = null;

      for (const milestone of milestones) {
        try {
          await createMilestone(createdProject.id, milestone);

          createdMilestones += 1;
        } catch (error) {
          failure = getApiErrorMessage(
            error,
            "A milestone could not be created.",
          );

          break;
        }
      }

      return {
        createdProject,
        createdMilestones,
        plannedMilestones: milestones.length,
        failure,
      };
    },

    onSuccess: async ({
      createdProject,
      createdMilestones,
      plannedMilestones,
      failure,
    }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.client(),
        }),

        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.all,
        }),
      ]);

      if (failure) {
        toast.warning(
          `"${createdProject.title}" was created with ${createdMilestones} of ` +
            `${plannedMilestones} milestones. ${failure} Add the rest from the project page.`,
        );
      } else {
        toast.success(`"${createdProject.title}" was created successfully.`);
      }

      router.replace("/dashboard/client/projects");

      router.refresh();
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Project could not be created."));
    },
  });
}
