export type ProjectStatus = "Open" | "InProgress" | "Completed" | "Cancelled";

export type ClientProject = {
  id: string;

  freelancerId: string | null;
  freelancerFullName: string | null;
  assignedFreelancers: { id: string; fullName: string }[];

  title: string;
  description: string;

  budget: number;

  budgetExact?: string;
  allocatedAmount: number;
  allocatedAmountExact?: string;

  milestoneCount: number;
  approvedMilestoneCount: number;
  submittedMilestoneCount: number;

  proposalCount: number;
  pendingProposalCount: number;
  hasEscrowHistory: boolean;

  deadline: string;
  status: ProjectStatus;
  createdAt: string;
};

export type GetClientProjectsParams = {
  page?: number;
  pageSize?: number;
  status?: ProjectStatus;
};

export type CreateProjectRequest = {
  title: string;
  description: string;
  budget: number;
  deadline: string;
};

export type CreatedProjectResponse = {
  id: string;
  title: string;
  description: string;
  budget: number;
  budgetExact?: string;
  deadline: string;
  status: ProjectStatus;
  createdAt: string;
};

export type AssignedProject = {
  id: string;
  clientId: string;
  clientFullName: string;
  title: string;
  description: string;
  budget: number;
  budgetExact?: string;
  allocatedAmount: number;
  allocatedAmountExact?: string;
  milestoneCount: number;
  rejectedMilestoneCount: number;
  deadline: string;
  status: ProjectStatus;
  createdAt: string;

  // Budget describes the whole project; this is the total of the stages
  // this freelancer holds, which is what they are paid.
  myTotalAmount: number;
  myTotalAmountExact?: string;
  isHistorical: boolean;
};

export type GetAssignedProjectsParams = {
  page?: number;
  pageSize?: number;
};
