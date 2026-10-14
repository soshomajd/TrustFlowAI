"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { markNotificationRead } from "@/features/notifications/api/notifications-api";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import type { NotificationsResponse } from "@/features/notifications/types/notification";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: markNotificationRead,
    onSuccess: (_, notificationId) => {
      queryClient.setQueryData<NotificationsResponse>(
        notificationQueryKeys.list(),
        (current) => {
          if (!current) return current;
          const items = current.items.filter((item) => item.id !== notificationId);
          return {
            items,
            actionCount: items.filter(
              (item) => item.severity !== "info" || item.canMarkRead,
            ).length,
          };
        },
      );
    },
    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The notification could not be marked as read. Please try again."));
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: notificationQueryKeys.all });
    },
  });
}
