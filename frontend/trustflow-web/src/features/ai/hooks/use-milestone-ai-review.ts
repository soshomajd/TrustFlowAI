"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import {
  createMilestoneAiReview,
  getMilestoneAiReview,
} from "@/features/ai/api/ai-api";
import { aiQueryKeys } from "@/features/ai/queries/ai-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useMilestoneAiReview(
  projectId: string,
  milestoneId: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: aiQueryKeys.review(projectId, milestoneId),
    queryFn: () => getMilestoneAiReview(projectId, milestoneId),
    enabled: enabled && Boolean(projectId && milestoneId),
  });
}

export function useRequestMilestoneAiReview(
  projectId: string,
  milestoneId: string,
) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => createMilestoneAiReview(projectId, milestoneId),

    onSuccess: (review) => {
      queryClient.setQueryData(
        aiQueryKeys.review(projectId, milestoneId),
        review,
      );
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "The AI review could not be made."),
      );
    },
  });
}
