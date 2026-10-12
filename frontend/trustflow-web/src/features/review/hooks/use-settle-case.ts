"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import {
  getAccount,
  readContract,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { toBaseUnits } from "@/features/escrow/lib/token-amount";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type SettleCaseVariables = {
  detail: ReviewCaseDetail;

  // What the freelancer receives from this milestone, in the token units a
  // person reads (for example dollars). The rest goes back to the client.
  freelancerAmount: number;

  // Also hand every later, unreleased milestone back to the client.
  endEscrow: boolean;
};

// The settlement is signed from the reviewer own wallet. Nothing on the
// server can do it: the contract only accepts its arbiter address. The checks
// before sending say, in words, why it would be refused.
export function useSettleCase() {
  return useMutation({
    mutationFn: async ({
      detail,
      freelancerAmount,
      endEscrow,
    }: SettleCaseVariables) => {
      if (!detail.contractAddress) {
        throw new Error("This escrow has no contract.");
      }

      const address = detail.contractAddress as Address;
      const chainId = detail.chainId;

      const account = getAccount(wagmiConfig);

      if (!account.address) {
        throw new Error("Connect your reviewer wallet first.");
      }

      const arbiter = await readContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "arbiter",
        chainId,
      });

      if (arbiter.toLowerCase() !== account.address.toLowerCase()) {
        throw new Error(
          "The connected wallet is not the arbiter of this escrow. Switch to the reviewer wallet.",
        );
      }

      if (
        freelancerAmount < 0 ||
        freelancerAmount > detail.case.milestoneAmount
      ) {
        throw new Error(
          "The freelancer amount must be between 0 and the milestone amount.",
        );
      }

      const hash = await writeContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "settleCase",
        args: [
          BigInt(detail.onChainMilestoneIndex),
          toBaseUnits(freelancerAmount, detail.tokenDecimals),
          endEscrow,
        ],
        chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, { hash, chainId });

      return hash;
    },

    onError: (error) => {
      toast.error(
        getWalletErrorMessage(error, "The settlement did not go through."),
      );
    },
  });
}
