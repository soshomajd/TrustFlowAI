export type ProposalStatus = "Pending" | "Accepted" | "Rejected" | "Withdrawn";

export type ClientProjectProposal = {
  id: string;
  projectId: string;

  freelancerId: string;
  freelancerFullName: string;

  coverLetter: string;
  bidAmount: number;
  estimatedDays: number;

  status: ProposalStatus;
  createdAt: string;
};

export type GetProjectProposalsParams = {
  page?: number;
  pageSize?: number;
};

export type CreateProposalRequest = {
  coverLetter: string;
  bidAmount: number;
  estimatedDays: number;
};

export type CreatedProposal = {
  id: string;
  projectId: string;
  freelancerId: string;

  coverLetter: string;
  bidAmount: number;
  estimatedDays: number;

  status: ProposalStatus;
  createdAt: string;
};

export type MyProposal = {
  id: string;
  projectId: string;
  projectTitle: string;

  coverLetter: string;
  bidAmount: number;
  estimatedDays: number;

  status: ProposalStatus;
  createdAt: string;
};

export type GetMyProposalsParams = {
  page?: number;
  pageSize?: number;
};

export type ProposalStatusResponse = {
  id: string;
  status: ProposalStatus;
};

import type { ProjectStatus } from "@/features/projects/types/project";

export type AcceptProposalResponse = {
  proposalId: string;
  projectId: string;
  freelancerId: string;

  proposalStatus: ProposalStatus;
  projectStatus: ProjectStatus;
};
