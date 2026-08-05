import type { ProjectStatus } from "@/features/projects/types/project";

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
export type MilestoneStatusResponse = {
  id: string;
  status: MilestoneStatus;
};
export type ApproveMilestoneResponse = {
  id: string;
  projectId: string;
  status: MilestoneStatus;
  projectStatus: ProjectStatus;
};

export type UpdateMilestoneRequest = CreateMilestoneRequest;
