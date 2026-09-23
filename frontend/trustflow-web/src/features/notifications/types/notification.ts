export type NotificationSeverity = "action" | "warning" | "info";

export type NotificationItem = {
  id: string;
  kind: string;
  severity: NotificationSeverity;
  title: string;
  message: string;
  href: string;
  projectTitle: string;
};

export type NotificationsResponse = {
  items: NotificationItem[];

  // Counts only action and warning items, so the badge means "waiting on
  // you" rather than "unread".
  actionCount: number;
};
