import type { ProjectStatus } from "@/features/projects/types/project";

export type PublicMarketplaceMilestone = {
  id: string;
  title: string;
  description: string;
  amount: number;
  amountExact?: string;
  sequenceNumber: number;
  deadline: string;
  isTaken: boolean;
};

export type MarketplaceProjectDetails = {
  id: string;
  clientId: string;
  clientFullName: string;
  title: string;
  description: string;
  budget: number;
  budgetExact?: string;
  allocatedAmount: number;
  allocatedAmountExact?: string;
  availableAmount: number;
  availableAmountExact?: string;
  deadline: string;
  createdAt: string;
  status: ProjectStatus;
  milestones: PublicMarketplaceMilestone[];
};
