"use client";
import { moneyText } from "@/lib/money/exact-money";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import {
  getAccount,
  getBlock,
  readContract,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { getSettlementBaseUnits } from "@/features/review/lib/settlement-amount";
import { settleWithRecovery } from "@/features/review/lib/settlement-recovery";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import { useAuthStore } from "@/stores/auth-store";
import type { ReviewCaseDetail } from "@/features/review/types/review";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type SettleCaseVariables = {
  detail: ReviewCaseDetail;

  // Gross freelancer award before the platform fee, in the token units a
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
      if (useAuthStore.getState().readOnly) {
        throw new Error("Local preview is read-only. Sign in with your administrator account to make a decision.");
      }
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

      const index = BigInt(detail.onChainMilestoneIndex);
      return settleWithRecovery({
        expectedKind: detail.case.kind === "PaymentRequest" ? 1 : 2,
        expectedOpenedAt: BigInt(Math.floor(new Date(detail.case.openedAt).getTime() / 1000)),
        index,
        read: async () => {
          const [stage, settled, open, nextIndex, block] = await Promise.all([
            readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "getMilestone", args: [index], chainId }),
            readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "isSettledByCase", args: [index], chainId }),
            readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "getOpenCase", chainId }),
            readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "nextMilestoneIndex", chainId }),
            getBlock(wagmiConfig, { chainId }),
          ]);
          return { released: stage[1], settled, kind: open[0], openedAt: open[1], nextIndex, timestamp: block.timestamp };
        },
        settle: () => confirmEscrowTransaction(
          `${chainId}:${address.toLowerCase()}:settle:${index}:${detail.case.openedAt}`,
          () => writeContract(wagmiConfig, { address, abi: escrowAbi, functionName: "settleCase", args: [index, amountBaseUnits, endEscrow], chainId }),
          (hash) => waitForTransactionReceipt(wagmiConfig, { hash, chainId }),
        ),
      });
    },

    onError: (error) => {
      toast.error(
        getWalletErrorMessage(error, "The settlement did not go through."),
      );
    },
  });
}
