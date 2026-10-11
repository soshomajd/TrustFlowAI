"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { readContract, waitForTransactionReceipt, writeContract } from "wagmi/actions";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { erc20Abi } from "@/features/escrow/lib/erc20-abi";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { toBaseUnits } from "@/features/escrow/lib/token-amount";
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

      const decimals = await readContract(wagmiConfig, {
        address: tokenAddress,
        abi: erc20Abi,
        functionName: "decimals",
        chainId: escrow.chainId,
      });

      const amount = toBaseUnits(escrow.totalAmount, decimals);

      const approveHash = await writeContract(wagmiConfig, {
        address: tokenAddress,
        abi: erc20Abi,
        functionName: "approve",
        args: [contractAddress, amount],
        chainId: escrow.chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, {
        hash: approveHash,
        chainId: escrow.chainId,
      });

      const fundHash = await writeContract(wagmiConfig, {
        address: contractAddress,
        abi: escrowAbi,
        functionName: "fund",
        chainId: escrow.chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, {
        hash: fundHash,
        chainId: escrow.chainId,
      });

      return syncEscrow(normalizedProjectId, freelancerId);
    },

    onSuccess: async (updatedEscrow) => {
      await queryClient.invalidateQueries({
        queryKey: escrowQueryKeys.participants(normalizedProjectId),
      });

      toast.success(
        updatedEscrow.status === "Funded"
          ? "Escrow funded on-chain."
          : "Funding transaction confirmed.",
      );
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Funding the escrow failed."));
    },
  });
}
