import "client-only";

import type { NotificationsResponse } from "@/features/notifications/types/notification";
import { apiClient } from "@/lib/api/api-client";

export async function getNotifications() {
  const response =
    await apiClient.get<NotificationsResponse>("/notifications");

  return response.data;
}
