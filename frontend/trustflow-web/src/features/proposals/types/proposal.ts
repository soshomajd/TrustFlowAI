export type ProposalStatus = "Pending" | "Accepted" | "Rejected" | "Withdrawn";

// The stages a proposal covers. Its price is their total, which is why the
// client compares the agreed price against these rather than the whole
// project budget: a partial proposal is never meant to equal it.
export type ProposalMilestone = {
  id: string;
  title: string;
  amount: number;
  sequenceNumber: number;
};

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

  milestones: ProposalMilestone[];
};

export type GetProjectProposalsParams = {
  page?: number;
  pageSize?: number;
};

export type CreateProposalRequest = {
  coverLetter: string;
  estimatedDays: number;
  milestoneIds: string[];
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

  milestones: ProposalMilestone[];
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

  milestones: ProposalMilestone[];
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
