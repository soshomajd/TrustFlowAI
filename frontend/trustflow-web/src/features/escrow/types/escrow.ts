export type EscrowStatus =
  | "PendingDeployment"
  | "Deploying"
  | "AwaitingFunding"
  | "Funded"
  | "Completed"
  | "Cancelled"
  | "Terminated";

export type ProjectEscrow = {
  id: string;
  projectId: string;

  // A project holds one escrow per assigned freelancer, because the contract
  // pays a single immutable address.
  freelancerId: string;

  chainId: number;
  tokenAddress: string;
  contractAddress: string | null;

  clientWalletAddress: string | null;
  freelancerWalletAddress: string | null;

  // The one address that can settle a case on this contract. It was fixed
  // into the contract when it was deployed.
  arbiterWalletAddress: string | null;

  totalAmount: number;

  totalAmountExact?: string;
  releasedAmount: number;
  releasedAmountExact?: string; // Gross awarded, including the platform fee.
  platformFeeBasisPoints?: number;
  feeRecipientAddress?: string | null;
  platformFeesPaid?: number;
  platformFeesPaidExact?: string;
  freelancerNetPaid?: number;
  freelancerNetPaidExact?: string;
  tokenDecimals?: number | null;

  status: EscrowStatus;

  deploymentTransactionHash: string | null;
  fundingTransactionHash: string | null;
  cancellationTransactionHash: string | null;

  createdAt: string;
  updatedAt: string;
  deployedAt: string | null;
  fundedAt: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
};

// One row per freelancer holding milestones on the project. The escrow is
// null until the client creates it, so the row still shows what is missing.
export type ProjectEscrowParticipant = {
  freelancerId: string;
  freelancerFullName: string;
  milestoneCount: number;
  assignedTotal: number;
  assignedTotalExact?: string;
  escrow: ProjectEscrow | null;
};

export type EscrowCaseKind = "PaymentRequest" | "RefundRequest";

export type EscrowCaseStatus = "Open" | "Withdrawn" | "Settled";

// A request on the contract that starts a 14 day clock. The chain decides;
// this is the backend's copy of it.
export type EscrowCase = {
  id: string;
  escrowId: string;
  projectId: string;
  freelancerId: string;
  milestoneId: string;
  milestoneTitle: string;
  milestoneAmount: number;
  milestoneAmountExact?: string;

  kind: EscrowCaseKind;
  openedByRole: "Freelancer" | "Client";
  status: EscrowCaseStatus;

  openedAt: string;

  // From here the reviewer may settle it.
  settleableAt: string;

  // From here either side may force a 50/50 split if the reviewer never
  // acted.
  stallableAt: string;

  closedAt: string | null;
  paidToFreelancer: number | null;
  paidToFreelancerExact?: string | null; // Gross award, kept for API compatibility.
  platformFeePaid?: number;
  platformFeePaidExact?: string;
  freelancerNetPaid?: number | null;
  freelancerNetPaidExact?: string | null;
  platformFeeBasisPoints?: number;
  returnedToClient: number | null;
  returnedToClientExact?: string | null;
  escrowEnded: boolean;
  reviewNote: string | null;

  chainId: number;
  contractAddress: string | null;
  arbiterWalletAddress: string | null;
};

export type CreateEscrowRequest = {
  freelancerId: string;
  chainId: number;
  tokenAddress: string;
};
