import type { GetClientProjectsParams } from "@/features/projects/types/project";

export const projectQueryKeys = {
  all: ["projects"] as const,

  client: () => [...projectQueryKeys.all, "client"] as const,

  clientLists: () => [...projectQueryKeys.client(), "list"] as const,

  clientList: (params: GetClientProjectsParams) =>
    [...projectQueryKeys.clientLists(), params] as const,

  clientDashboardSummary: () =>
    [...projectQueryKeys.client(), "dashboard-summary"] as const,
};
