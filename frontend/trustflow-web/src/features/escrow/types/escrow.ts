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

export type CreateEscrowRequest = {
  chainId: number;
  tokenAddress: string;
};
