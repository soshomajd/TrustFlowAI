export type NotificationSeverity = "action" | "warning" | "info";

export type NotificationItem = {
  id: string;
  kind: string;
  severity: NotificationSeverity;
  title: string;
  message: string;
  href: string;
  projectTitle: string;
  canMarkRead: boolean;
};

export type NotificationsResponse = {
  items: NotificationItem[];

  // Actions/warnings plus unread durable notices waiting to be seen.
  actionCount: number;
};
