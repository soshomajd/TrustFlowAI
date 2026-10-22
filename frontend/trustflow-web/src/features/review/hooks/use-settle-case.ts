"use client";
import { moneyText } from "@/lib/money/exact-money";

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
import { getSettlementBaseUnits } from "@/features/review/lib/settlement-amount";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type SettleCaseVariables = {
  detail: ReviewCaseDetail;

  // What the freelancer receives from this milestone, in the token units a
  // person reads (for example dollars). The rest goes back to the client.
  freelancerAmount: string;

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

      if (!Number.isSafeInteger(detail.onChainMilestoneIndex) || detail.onChainMilestoneIndex < 0) {
        throw new Error("The milestone position is invalid. Refresh this case.");
      }
      const milestone = await readContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "getMilestone",
        args: [BigInt(detail.onChainMilestoneIndex)],
        chainId,
      });
      const amountBaseUnits = getSettlementBaseUnits(
        freelancerAmount, moneyText(detail.case, "milestoneAmount"),
        detail.tokenDecimals, milestone[0],
      );

      const hash = await writeContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "settleCase",
        args: [
          BigInt(detail.onChainMilestoneIndex),
          amountBaseUnits,
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
