"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import {
  adviseReviewCase,
  getReviewCase,
  listReviewCases,
  saveReviewNote,
  syncReviewCase,
} from "@/features/review/api/review-api";
import { reviewQueryKeys } from "@/features/review/queries/review-query-keys";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { isValidGuid } from "@/lib/validation/is-valid-guid";

export function useReviewCases(status: "open" | "closed") {
  return useQuery({
    queryKey: reviewQueryKeys.list(status),
    queryFn: () => listReviewCases(status),
    refetchInterval: 60_000,
  });
}

export function useReviewCase(caseId: string) {
  return useQuery({
    queryKey: reviewQueryKeys.detail(caseId),
    queryFn: () => getReviewCase(caseId),
    enabled: isValidGuid(caseId),
  });
}

// The detail comes back from every action, so the screen is replaced with it
// instead of being fetched again.
function useStoreDetail(caseId: string) {
  const queryClient = useQueryClient();

  return async (detail: ReviewCaseDetail) => {
    queryClient.setQueryData(reviewQueryKeys.detail(caseId), detail);

    await Promise.all([
      queryClient.invalidateQueries({
        queryKey: [...reviewQueryKeys.all, "list"],
      }),
      queryClient.invalidateQueries({ queryKey: notificationQueryKeys.all }),
    ]);
  };
}

export function useSyncReviewCase(caseId: string) {
  const store = useStoreDetail(caseId);

  return useMutation({
    mutationFn: () => syncReviewCase(caseId),
    onSuccess: store,
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The contract could not be read."));
    },
  });
}

export function useAdviseReviewCase(caseId: string) {
  const store = useStoreDetail(caseId);

  return useMutation({
    mutationFn: () => adviseReviewCase(caseId),
    onSuccess: store,
    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "The AI could not give advice right now."),
      );
    },
  });
}

export function useSaveReviewNote(caseId: string) {
  const store = useStoreDetail(caseId);

  return useMutation({
    mutationFn: (note: string) => saveReviewNote(caseId, note),
    onSuccess: store,
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The note could not be saved."));
    },
  });
}
