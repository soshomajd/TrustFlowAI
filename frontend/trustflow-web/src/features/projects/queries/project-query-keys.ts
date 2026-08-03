import type { GetClientProjectsParams } from "@/features/projects/types/project";
import type { NormalizedMarketplaceProjectsParams } from "@/features/projects/types/marketplace-project";

export const projectQueryKeys = {
  all: ["projects"] as const,

  client: () => [...projectQueryKeys.all, "client"] as const,

  clientLists: () => [...projectQueryKeys.client(), "list"] as const,

  clientList: (params: GetClientProjectsParams) =>
    [...projectQueryKeys.clientLists(), params] as const,

  clientDashboardSummary: () =>
    [...projectQueryKeys.client(), "dashboard-summary"] as const,

  create: () => [...projectQueryKeys.all, "create"] as const,

  workspace: (projectId: string) =>
    [...projectQueryKeys.all, "workspace", projectId] as const,
  marketplaceLists: () => [...projectQueryKeys.all, "marketplace"] as const,

  marketplaceList: (params: NormalizedMarketplaceProjectsParams) =>
    [...projectQueryKeys.marketplaceLists(), params] as const,
};
