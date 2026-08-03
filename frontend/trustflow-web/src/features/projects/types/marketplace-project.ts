import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectMarketplaceSortOption =
  | "Newest"
  | "Oldest"
  | "BudgetLowToHigh"
  | "BudgetHighToLow"
  | "DeadlineSoonest"
  | "DeadlineLatest";

export type MarketplaceProject = {
  id: string;

  title: string;
  description: string;

  budget: number;
  allocatedAmount: number;
  milestoneCount: number;

  deadline: string;
  createdAt: string;

  status: ProjectStatus;
};

export type GetMarketplaceProjectsParams = {
  page?: number;
  pageSize?: number;

  search?: string;

  minBudget?: number;
  maxBudget?: number;

  deadlineBefore?: string;

  sortBy?: ProjectMarketplaceSortOption;
};

export type NormalizedMarketplaceProjectsParams = {
  page: number;
  pageSize: number;

  search?: string;

  minBudget?: number;
  maxBudget?: number;

  deadlineBefore?: string;

  sortBy: ProjectMarketplaceSortOption;
};
