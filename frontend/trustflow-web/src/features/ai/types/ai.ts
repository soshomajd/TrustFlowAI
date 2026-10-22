export type AiStatus = {
  enabled: boolean;

  // True while the fixed placeholder model is answering.
  isDemo: boolean;
};

// ---- project drafting -------------------------------------------------

export type ProjectDraftRequest = {
  idea: string;
  budgetHint?: number;
  deadlineDays?: number;
};

export type ProjectDraftMilestone = {
  sequenceNumber: number;
  title: string;
  description: string;
  amount: number;
  amountExact?: string;
  dueInDays: number;
};

export type ProjectDraft = {
  title: string;
  description: string;
  budget: number;
  budgetExact?: string;
  priceRationale: string;
  milestones: ProjectDraftMilestone[];
  assumptions: string[];
  questionsForClient: string[];
  isDemo: boolean;
};

// ---- delivery review --------------------------------------------------

export type CriterionStatus = "met" | "partially_met" | "not_met" | "unclear";

export type AiCriterion = {
  requirement: string;
  status: CriterionStatus;
  evidence: string;
};

export type ReviewRecommendation =
  | "Approve"
  | "RequestChanges"
  | "InsufficientEvidence";

export type MilestoneAiReview = {
  submissionId: string;
  submissionVersion: number;
  recommendation: ReviewRecommendation;
  confidence: number;
  summary: string;
  criteria: AiCriterion[];
  suggestedFeedback: string;
  limitations: string[];
  createdAt: string;
  isDemo: boolean;
};

// ---- freelancer profile and resume -------------------------------------

export type ProfileGap = {
  field: string;
  advice: string;
};

export type SuggestedPortfolioItem = {
  title: string;
  description: string;
};

export type ProfileCoaching = {
  completenessScore: number;
  summary: string;
  missing: ProfileGap[];
  suggestedHeadline: string;
  suggestedAbout: string;
  suggestedSkills: string[];
  suggestedPortfolioItems: SuggestedPortfolioItem[];
  isDemo: boolean;
};

export type ResumeDraftRequest = {
  extraFacts?: string;
};

export type ResumeDraft = {
  resumeMarkdown: string;
  notes: string[];
  isDemo: boolean;
};
