import type { AiCriterion } from "@/features/ai/types/ai";
import type {
  EscrowCaseKind,
  EscrowCaseStatus,
} from "@/features/escrow/types/escrow";
import type { MilestoneSubmission } from "@/features/milestones/types/milestone";

export type ReviewCaseSummary = {
  id: string;
  projectId: string;
  projectTitle: string;
  milestoneTitle: string;
  milestoneAmount: number;

  kind: EscrowCaseKind;
  openedByRole: "Freelancer" | "Client";
  status: EscrowCaseStatus;

  openedAt: string;
  settleableAt: string;

  // Open, and the 14 days are over.
  isReady: boolean;

  hasAdvice: boolean;
  escrowEnded: boolean;
  paidToFreelancer: number | null; // Gross award.
  platformFeePaid?: number;
  freelancerNetPaid?: number | null;
  returnedToClient: number | null;
  closedAt: string | null;
};

export type ReviewAdvice = {
  freelancerSharePercent: number;
  confidence: number;
  summary: string;
  findings: AiCriterion[];
  reasoning: string;
  manipulationAttempts: string[];
  createdAt: string;
  isDemo: boolean;
};

export type ReviewStatements = {
  openedByRole: "Freelancer" | "Client";
  openerStatement: string;
  respondentStatement: string | null;
  status: string;
  rulingFreelancerSharePercent: number | null;
  rulingSummary: string | null;
};

export type ReviewCaseDetail = {
  case: ReviewCaseSummary;

  projectDescription: string;
  milestoneDescription: string;
  milestoneSequence: number;
  milestoneDeadline: string;
  deadlinePassed: boolean;
  milestoneStatus: string;

  chainId: number;
  contractAddress: string | null;
  arbiterWalletAddress: string | null;
  tokenAddress: string;
  tokenDecimals: number;
  platformFeeBasisPoints?: number;

  // The milestone position on the contract: the first argument of
  // settleCase.
  onChainMilestoneIndex: number;

  deliveries: MilestoneSubmission[];
  dispute: ReviewStatements | null;
  advice: ReviewAdvice | null;
  reviewNote: string | null;
  aiEnabled: boolean;
};
