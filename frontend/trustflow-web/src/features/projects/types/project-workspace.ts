import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectWorkspaceMilestone = {
  id: string;
  projectId: string;
  title: string;
  description: string;
  amount: number;
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
  platformFeePaid?: number;
  freelancerNetPaid?: number | null;

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
  deadline: string;
  status: ProjectStatus;
  createdAt: string;
  activeProposalCount: number;

  milestones: ProjectWorkspaceMilestone[];
};
