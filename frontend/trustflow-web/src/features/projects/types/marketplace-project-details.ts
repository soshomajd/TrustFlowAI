import type { ProjectStatus } from "@/features/projects/types/project";

export type PublicMarketplaceMilestone = {
  id: string;
  title: string;
  description: string;
  amount: number;
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
  allocatedAmount: number;
  availableAmount: number;
  deadline: string;
  createdAt: string;
  status: ProjectStatus;
  milestones: PublicMarketplaceMilestone[];
};
