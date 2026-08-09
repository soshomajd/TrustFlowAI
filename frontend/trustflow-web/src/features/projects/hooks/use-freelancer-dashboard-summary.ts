"use client";

import { useQuery } from "@tanstack/react-query";

import { getFreelancerDashboardSummary } from "@/features/projects/api/projects-api";

export const freelancerDashboardSummaryQueryKey = [
  "projects",
  "freelancer-dashboard-summary",
] as const;

export function useFreelancerDashboardSummary() {
  return useQuery({
    queryKey: freelancerDashboardSummaryQueryKey,

    queryFn: ({ signal }) => getFreelancerDashboardSummary(signal),

    staleTime: 15_000,

    refetchInterval: 15_000,

    refetchIntervalInBackground: false,

    refetchOnWindowFocus: true,

    retry: 1,
  });
}
