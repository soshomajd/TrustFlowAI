export type ProjectStatus = "Open" | "InProgress" | "Completed" | "Cancelled";

export type ClientProject = {
  id: string;

  freelancerId: string | null;
  freelancerFullName: string | null;
  assignedFreelancers: { id: string; fullName: string }[];

  title: string;
  description: string;

  budget: number;
  allocatedAmount: number;

  milestoneCount: number;
  approvedMilestoneCount: number;
  submittedMilestoneCount: number;

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
  allocatedAmount: number;
  milestoneCount: number;
  rejectedMilestoneCount: number;
  deadline: string;
  status: ProjectStatus;
  createdAt: string;

  // Budget describes the whole project; this is the total of the stages
  // this freelancer holds, which is what they are paid.
  myTotalAmount: number;
  isHistorical: boolean;
};

export type GetAssignedProjectsParams = {
  page?: number;
  pageSize?: number;
};
