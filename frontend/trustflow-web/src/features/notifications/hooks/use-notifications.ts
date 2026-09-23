"use client";

import { useQuery } from "@tanstack/react-query";

import { getNotifications } from "@/features/notifications/api/notifications-api";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import { useAuthStore } from "@/stores/auth-store";

export function useNotifications() {
  const isAuthenticated = useAuthStore((state) => Boolean(state.user));

  return useQuery({
    queryKey: notificationQueryKeys.list(),
    queryFn: getNotifications,
    enabled: isAuthenticated,

    // Derived from live workflow state, so a short refetch keeps the badge
    // close to reality without a websocket.
    staleTime: 15_000,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}
