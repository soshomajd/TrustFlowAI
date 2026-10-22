export type AdminDisputeKind = "escrow" | "written";
export type AdminDisputeSummary = {
  id: string; recordType: AdminDisputeKind; milestoneId: string; milestoneTitle: string; milestoneAmountExact: string;
  caseKind: string | null; status: string; openedByRole: string; clientId: string | null; clientName: string;
  freelancerId: string | null; freelancerName: string; openedAt: string; closedAt: string | null;
  reviewableAt: string | null; fallbackAt: string | null;
};
export type AdminDisputes = { items: AdminDisputeSummary[]; page: number; pageSize: number; totalItems: number; totalPages: number; hasPreviousPage: boolean; hasNextPage: boolean };
export type AdminDisputeDetail = {
  record: AdminDisputeSummary; escrowId: string | null; chainId: number | null; contractAddress: string | null;
  tokenAddress: string | null; arbiterWalletAddress: string | null; clientWalletAddress: string | null; freelancerWalletAddress: string | null;
  openerStatement: string | null; respondentStatement: string | null; respondentStatementAt: string | null;
  reviewNote: string | null; reviewNoteAt: string | null;
  outcome: { grossAmountExact: string | null; platformFeePaidExact: string | null; freelancerNetPaidExact: string | null; returnedToClientExact: string | null; escrowEnded: boolean } | null;
  opinion: { readable: boolean; summary: string | null; reasoning: string | null; freelancerSharePercent: number | null; verdict: string | null; recordedAt: string | null; isDemo: boolean | null } | null;
};
