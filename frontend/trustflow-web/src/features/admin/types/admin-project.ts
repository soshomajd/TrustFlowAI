import type { ProjectStatus } from "@/features/projects/types/project";

export type AdminProject = {
  id: string;
  title: string;
  status: ProjectStatus;
  clientId: string | null;
  clientName: string;
  budgetExact: string;
  createdAt: string;
  milestoneCount: number;
  escrowCount: number;
  proposalCount: number;
  openCaseCount: number;
  freelancers: { id: string; fullName: string }[];
};

export type AdminProjectQuery = {
  page: number;
  pageSize: number;
  search?: string;
  status?: ProjectStatus;
  clientId?: string;
  freelancerId?: string;
  from?: string;
  to?: string;
};

export type AdminProjectsPage = {
  items: AdminProject[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
};
