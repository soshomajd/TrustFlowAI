import type { ProjectStatus } from "@/features/projects/types/project";

export type AdminDossier = {
  id: string; title: string; description: string; status: ProjectStatus;
  clientId: string | null; clientName: string; budgetExact: string;
  deadline: string; createdAt: string; participantCount: number;
  milestoneCount: number; proposalCount: number; escrowCount: number; deliveryCount: number;
};
export type DossierSection = "participants" | "milestones" | "proposals" | "escrows" | "deliveries" | "finance" | "conversations" | "disputes";
export type DossierParticipant = {
  kind: "participant"; id: string; fullName: string; isClient: boolean;
  currentFreelancer: boolean; hasEscrow: boolean; hasProposal: boolean;
  walletAddress: string | null; walletVerified: boolean; hasDelivery: boolean;
};
export type DossierMilestone = {
  kind: "milestone"; id: string; title: string; description: string;
  sequenceNumber: number; status: string; amountExact: string; deadline: string;
  freelancerId: string | null; freelancerName: string | null;
};
export type DossierProposal = {
  kind: "proposal"; id: string; freelancerId: string; freelancerName: string;
  status: string; coverLetter: string; bidAmountExact: string; estimatedDays: number;
  milestoneCount: number; createdAt: string;
};
export type DossierEscrow = {
  kind: "escrow"; id: string; freelancerId: string; freelancerName: string;
  status: string; chainId: number; tokenAddress: string; contractAddress: string | null;
  clientWalletAddress: string | null; freelancerWalletAddress: string | null;
  arbiterWalletAddress: string | null; feeRecipientAddress: string | null;
  platformFeeBasisPoints: number; tokenDecimals: number | null;
  totalAmountExact: string; releasedAmountExact: string; createdAt: string; updatedAt: string;
};
export type DossierDelivery = {
  kind: "delivery"; id: string; milestoneId: string; milestoneTitle: string;
  sequenceNumber: number; currentMilestoneStatus: string; freelancerId: string;
  freelancerName: string; version: number; isLatest: boolean; note: string;
  previewLink: string | null; sourceFilesLink: string | null; clientFeedback: string | null;
  submittedAt: string; reviewedAt: string | null;
  attachments: { id: string; fileName: string; contentType: string; fileSize: number; sha256: string }[];
};
export type DossierRow = DossierParticipant | DossierMilestone | DossierProposal | DossierEscrow | DossierDelivery;
export type DossierRecords = {
  items: DossierRow[]; page: number; pageSize: number; totalItems: number;
  totalPages: number; hasPreviousPage: boolean; hasNextPage: boolean;
};
