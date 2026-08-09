import type { ProjectStatus } from "@/features/projects/types/project";

export type ProjectWorkspaceMilestone = {
  id: string;
  projectId: string;
  title: string;
  description: string;
  amount: number;
  sequenceNumber: number;
  deadline: string;
  status: "Pending" | "InProgress" | "Submitted" | "Approved" | "Rejected";
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

  milestones: ProjectWorkspaceMilestone[];
};
