"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import {
  listMilestoneDisputes,
  openDispute,
  requestDisputeRuling,
  respondToDispute,
} from "@/features/ai/api/ai-api";
import { aiQueryKeys } from "@/features/ai/queries/ai-query-keys";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useMilestoneDisputes(
  projectId: string,
  milestoneId: string,
  enabled: boolean,
) {
  return useQuery({
    queryKey: aiQueryKeys.disputes(projectId, milestoneId),
    queryFn: () => listMilestoneDisputes(projectId, milestoneId),
    enabled: enabled && Boolean(projectId && milestoneId),
  });
}

// Every dispute action changes what this milestone's panel and the
// notification bell show, and a ruling or a new dispute also changes which
// buttons the milestone itself offers.
function useRefreshAfterDisputeChange(
  projectId: string,
  milestoneId: string,
) {
  const queryClient = useQueryClient();

  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: aiQueryKeys.disputes(projectId, milestoneId),
      }),

      queryClient.invalidateQueries({
        queryKey: notificationQueryKeys.all,
      }),

      queryClient.invalidateQueries({
        queryKey: milestoneQueryKeys.projectList(projectId),
      }),
    ]);
  };
}

export function useOpenDispute(projectId: string, milestoneId: string) {
  const refresh = useRefreshAfterDisputeChange(projectId, milestoneId);

  return useMutation({
    mutationFn: (statement: string) =>
      openDispute(projectId, milestoneId, statement),

    onSuccess: async () => {
      await refresh();

      toast.success("Dispute opened. The other side can now write their side.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The dispute could not be opened."));
    },
  });
}

export function useRespondToDispute(projectId: string, milestoneId: string) {
  const refresh = useRefreshAfterDisputeChange(projectId, milestoneId);

  return useMutation({
    mutationFn: (input: { disputeId: string; statement: string }) =>
      respondToDispute(input.disputeId, input.statement),

    onSuccess: async () => {
      await refresh();

      toast.success("Your statement was saved.");
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Your statement could not be saved."),
      );
    },
  });
}

export function useRequestDisputeRuling(
  projectId: string,
  milestoneId: string,
) {
  const refresh = useRefreshAfterDisputeChange(projectId, milestoneId);

  return useMutation({
    mutationFn: (disputeId: string) => requestDisputeRuling(disputeId),

    onSuccess: async (dispute) => {
      await refresh();

      toast.success(
        dispute.status === "Ruled"
          ? "The AI ruling is ready."
          : "The AI could not decide on this evidence.",
      );
    },

    onError: async (error) => {
      // A failed try can still change what the panel should offer, such
      // as how many tries are left.
      await refresh();

      toast.error(getApiErrorMessage(error, "The ruling could not be made."));
    },
  });
}
