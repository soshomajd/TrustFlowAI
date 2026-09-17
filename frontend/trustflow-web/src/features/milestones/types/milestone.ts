import type { ProjectStatus } from "@/features/projects/types/project";

export type MilestoneStatus =
  | "Pending"
  | "InProgress"
  | "Submitted"
  | "Approved"
  | "Rejected";

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
