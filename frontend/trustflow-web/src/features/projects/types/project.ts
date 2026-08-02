export type ProjectStatus = "Open" | "InProgress" | "Completed" | "Cancelled";

export type ClientProject = {
  id: string;

  freelancerId: string | null;
  freelancerFullName: string | null;

  title: string;
  description: string;

  budget: number;
  allocatedAmount: number;

  milestoneCount: number;
  approvedMilestoneCount: number;

  proposalCount: number;
  pendingProposalCount: number;

  deadline: string;
  status: ProjectStatus;
  createdAt: string;
};

export type GetClientProjectsParams = {
  page?: number;
  pageSize?: number;
  status?: ProjectStatus;
};
