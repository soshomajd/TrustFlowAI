import "client-only";

import type {
  MarketplaceProject,
  NormalizedMarketplaceProjectsParams,
} from "@/features/projects/types/marketplace-project";
import { apiClient } from "@/lib/api/api-client";
import type { PagedResponse } from "@/types/pagination";

export async function getMarketplaceProjects(
  params: NormalizedMarketplaceProjectsParams,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<PagedResponse<MarketplaceProject>>(
    "/projects",
    {
      params,
      signal,
    },
  );

  return response.data;
}
