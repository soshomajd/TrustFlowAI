import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectWorkspaceMilestone = {
  id: string;
  projectId: string;
  title: string;
  description: string;
  amount: number;
  amountExact?: string;
  sequenceNumber: number;
  deadline: string;
  status:
    | "Pending"
    | "InProgress"
    | "Submitted"
    | "Approved"
    | "Rejected"
    | "Settled";

  paidAmount: number | null;

  paidAmountExact?: string | null;
  platformFeePaid?: number;
  platformFeePaidExact?: string;
  freelancerNetPaid?: number | null;
  freelancerNetPaidExact?: string | null;

  // Stages are assigned one freelancer at a time, so a split project has
  // several owners across its milestones.
  freelancerId: string | null;
  freelancerFullName: string | null;
};

export type ProjectWorkspace = {
  id: string;
  clientId: string | null;
  clientFullName: string;
  freelancerId: string | null;
  freelancerFullName: string | null;
  title: string;
  description: string;
  budget: number;
  budgetExact?: string;
  deadline: string;
  status: ProjectStatus;
  createdAt: string;
  activeProposalCount: number;

  milestones: ProjectWorkspaceMilestone[];
};
