import type { ProjectStatus } from "@/features/projects/types/project";

export type MilestoneStatus =
  | "Pending"
  | "InProgress"
  | "Submitted"
  | "Approved"
  | "Rejected"
  | "Settled";

export type MilestoneSubmissionAttachment = {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  sha256: string;
};

export type MilestoneSubmission = {
  id: string;
  milestoneId: string;
  version: number;
  note: string;
  previewLink: string | null;
  sourceFilesLink: string | null;
  clientFeedback: string | null;
  submittedAt: string;
  reviewedAt: string | null;
  attachments: MilestoneSubmissionAttachment[];
};

export type SubmitMilestoneInput = {
  note: string;
  previewLink: string;
  sourceFilesLink: string;
  files: File[];
};

export type ProjectMilestone = {
  id: string;
  projectId: string;

  title: string;
  description: string;

  amount: number;
  sequenceNumber: number;

  deadline: string;
  status: MilestoneStatus;

  // Only set once a reviewer settled the milestone: what the freelancer
  // actually received for it, which can be less than the amount.
  paidAmount: number | null;
  platformFeePaid?: number;
  freelancerNetPaid?: number | null;

  // Stages are assigned one freelancer at a time, and each is paid by that
  // freelancer's own escrow contract.
  freelancerId: string | null;
  freelancerFullName: string | null;
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
