"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { getAccount, readContract, waitForTransactionReceipt, writeContract } from "wagmi/actions";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { erc20Abi } from "@/features/escrow/lib/erc20-abi";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { fundEscrowWithRecovery } from "@/features/escrow/lib/recoverable-escrow-actions";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export function useFundEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.fund(normalizedProjectId, freelancerId),

    mutationFn: async (escrow: ProjectEscrow) => {
      if (!escrow.contractAddress) {
        throw new Error("The escrow contract has not been deployed yet.");
      }

      const tokenAddress = escrow.tokenAddress as Address;
      const contractAddress = escrow.contractAddress as Address;

      const clientAddress = escrow.clientWalletAddress as Address | null;
      const key = `${escrow.chainId}:${contractAddress.toLowerCase()}`;
      const wait = (hash: `0x${string}`) => waitForTransactionReceipt(wagmiConfig, {
        hash,
        chainId: escrow.chainId,
      });

      return fundEscrowWithRecovery({
        readState: () => readContract(wagmiConfig, {
          address: contractAddress,
          abi: escrowAbi,
          functionName: "escrowState",
          chainId: escrow.chainId,
        }),
        readFunding: async () => {
          if (!clientAddress || getAccount(wagmiConfig).address?.toLowerCase() !== clientAddress.toLowerCase()) {
            throw new Error("Connect the client's verified wallet before funding.");
          }
          const [amount, token, client, allowance] = await Promise.all([
            readContract(wagmiConfig, { address: contractAddress, abi: escrowAbi, functionName: "totalAmount", chainId: escrow.chainId }),
            readContract(wagmiConfig, { address: contractAddress, abi: escrowAbi, functionName: "token", chainId: escrow.chainId }),
            readContract(wagmiConfig, { address: contractAddress, abi: escrowAbi, functionName: "client", chainId: escrow.chainId }),
            readContract(wagmiConfig, { address: tokenAddress, abi: erc20Abi, functionName: "allowance", args: [clientAddress, contractAddress], chainId: escrow.chainId }),
          ]);
          if (token.toLowerCase() !== tokenAddress.toLowerCase() || client.toLowerCase() !== clientAddress.toLowerCase()) {
            throw new Error("The escrow token or client does not match this project. Refresh its status before continuing.");
          }
          // The immutable contract amount is already exact integer base units.
          return { amount, allowance };
        },
        approve: (amount) => confirmEscrowTransaction(`${key}:approve`, () => writeContract(wagmiConfig, {
          address: tokenAddress,
          abi: erc20Abi,
          functionName: "approve",
          args: [contractAddress, amount],
          account: clientAddress!,
          chainId: escrow.chainId,
        }), wait),
        fund: () => confirmEscrowTransaction(`${key}:fund`, () => writeContract(wagmiConfig, {
          address: contractAddress,
          abi: escrowAbi,
          functionName: "fund",
          account: clientAddress!,
          chainId: escrow.chainId,
        }), wait),
        sync: () => syncEscrow(normalizedProjectId, freelancerId),
      });
    },

    onSuccess: async (updatedEscrow) => {
      await invalidateEscrowWorkflow(queryClient, normalizedProjectId);

      if (updatedEscrow.status === "Funded") {
        toast.success("Escrow funded on-chain.");
      } else {
        toast.info("Escrow status refreshed.");
      }
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Funding the escrow failed."));
    },
  });
}
