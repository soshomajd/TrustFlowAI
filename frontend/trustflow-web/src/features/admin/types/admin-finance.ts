export type FinanceEscrow = {
  workStartedAt?: string | null; workStartTransactionHash?: string | null;
  id: string; freelancerId: string; freelancerName: string; status: string; chainId: number;
  tokenAddress: string; contractAddress: string | null; tokenDecimals: number | null;
  totalAmountExact: string; fundingAmountExact: string | null; grossReleasedExact: string;
  platformFeesPaidExact: string; freelancerNetPaidExact: string | null;
  recordedCaseRefundsExact: string; cancellationRefundExact: string | null;
  remainingRecordedAmountExact: string | null; gaps: string[];
  deploymentTransactionHash: string | null; fundingTransactionHash: string | null;
  cancellationTransactionHash: string | null; fundedAt: string | null; cancelledAt: string | null; updatedAt: string;
};
export type FinancePayout = {
  kind: "payout"; id: string; title: string; status: string; grossAmountExact: string | null;
  platformFeePaidExact: string | null; freelancerNetPaidExact: string | null;
  caseId: string | null; recordedAt: string | null; paymentTransactionHash?: string | null; paymentAt?: string | null;
};
export type FinanceSettlement = {
  kind: "settlement"; id: string; milestoneId: string; milestoneTitle: string;
  grossAmountExact: string | null; platformFeePaidExact: string | null;
  freelancerNetPaidExact: string | null; returnedToClientExact: string | null;
  escrowEnded: boolean; closedAt: string | null; paymentTransactionHash?: string | null; paymentAt?: string | null;
};
export type FinancePage<T> = { items: T[]; page: number; pageSize: number; totalItems: number;
  totalPages: number; hasPreviousPage: boolean; hasNextPage: boolean };
export type FinanceSection = "payouts" | "settlements";
