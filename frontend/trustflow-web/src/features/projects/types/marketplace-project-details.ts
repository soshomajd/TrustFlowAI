import type { ProjectStatus } from "@/features/projects/types/project";

export type PublicMarketplaceMilestone = {
  id: string;
  title: string;
  description: string;
  amount: number;
  sequenceNumber: number;
  deadline: string;
};

export type MarketplaceProjectDetails = {
  id: string;
  title: string;
  description: string;
  budget: number;
  allocatedAmount: number;
  deadline: string;
  createdAt: string;
  status: ProjectStatus;
  milestones: PublicMarketplaceMilestone[];
};
