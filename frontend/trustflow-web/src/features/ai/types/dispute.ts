import type { AiCriterion } from "@/features/ai/types/ai";

export type DisputeStatus = "Open" | "Ruled" | "Inconclusive" | "Settled";

export type DisputeVerdict =
  | "FavorClient"
  | "FavorFreelancer"
  | "Split"
  | "Inconclusive";

export type DisputeSide = "Client" | "Freelancer";

export type DisputeRuling = {
  verdict: DisputeVerdict;
  freelancerSharePercent: number;
  clientSharePercent: number;
  freelancerAmount: number;
  clientAmount: number;
  confidence: number;
  summary: string;
  findings: AiCriterion[];
  reasoning: string;
  manipulationAttempts: string[];
  ruledAt: string;
};

export type Dispute = {
  id: string;
  projectId: string;
  milestoneId: string;
  milestoneTitle: string;
  milestoneAmount: number;
  status: DisputeStatus;
  myRole: DisputeSide;
  openedByRole: DisputeSide;
  openerStatement: string;
  respondentStatement: string | null;
  respondentStatementAt: string | null;
  createdAt: string;
  responseDeadline: string;
  canRespond: boolean;
  canRequestRuling: boolean;
  ruling: DisputeRuling | null;
  isDemo: boolean;
  notice: string;
};
