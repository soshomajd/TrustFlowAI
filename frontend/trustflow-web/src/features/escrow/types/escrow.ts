export type EscrowStatus =
  | "PendingDeployment"
  | "Deploying"
  | "AwaitingFunding"
  | "Funded"
  | "Completed"
  | "Cancelled";

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

  totalAmount: number;
  releasedAmount: number;

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
  escrow: ProjectEscrow | null;
};

export type CreateEscrowRequest = {
  freelancerId: string;
  chainId: number;
  tokenAddress: string;
};
