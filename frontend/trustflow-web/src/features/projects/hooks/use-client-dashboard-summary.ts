"use client";

import { useQuery } from "@tanstack/react-query";

import { getClientDashboardSummary } from "@/features/projects/api/projects-api";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";

export function useClientDashboardSummary() {
  return useQuery({
    queryKey: projectQueryKeys.clientDashboardSummary(),

    queryFn: () => getClientDashboardSummary(),
  });
}
