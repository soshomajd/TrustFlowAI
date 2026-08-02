export type MilestoneStatus =
  | "Pending"
  | "InProgress"
  | "Submitted"
  | "Approved"
  | "Rejected";

export type ProjectMilestone = {
  id: string;
  projectId: string;

  title: string;
  description: string;

  amount: number;
  sequenceNumber: number;

  deadline: string;
  status: MilestoneStatus;
};

export type CreateMilestoneRequest = {
  title: string;
  description: string;
  amount: number;
  sequenceNumber: number;
  deadline: string;
};
