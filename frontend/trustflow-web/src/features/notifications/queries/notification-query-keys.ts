export const notificationQueryKeys = {
  all: ["notifications"] as const,

  list: () => [...notificationQueryKeys.all, "list"] as const,
};
