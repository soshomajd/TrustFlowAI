import "client-only";

import type { MarketplaceProjectDetails } from "@/features/projects/types/marketplace-project-details";
import { apiClient } from "@/lib/api/api-client";

export async function getMarketplaceProjectDetails(
  projectId: string,
  signal?: AbortSignal,
) {
  const response = await apiClient.get<MarketplaceProjectDetails>(
    `/projects/${projectId}`,
    {
      signal,
    },
  );

  return response.data;
}
