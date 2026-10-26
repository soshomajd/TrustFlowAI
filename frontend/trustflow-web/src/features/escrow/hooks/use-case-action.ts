"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import {
  getAccount,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { invalidateEscrowWorkflow } from "@/features/escrow/queries/invalidate-escrow-workflow";
import { readCaseChainState } from "@/features/escrow/lib/read-case-chain-state";
import { runCaseActionWithRecovery, type CaseAction, type ExpectedCase } from "@/features/escrow/lib/recoverable-case-actions";
export type { CaseAction } from "@/features/escrow/lib/recoverable-case-actions";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type CaseActionVariables = {
  escrow: ProjectEscrow;
  action: CaseAction;

  // The position of the milestone among this freelancer's stages. A request
  // always concerns the next unpaid one.
  onChainIndex?: number;
  expectedCase?: ExpectedCase;
};

const successMessages: Record<CaseAction, string> = {
  requestPayment:
    "Your request is on the contract. A person can settle it after 14 days.",
  requestRefund:
    "Your request is on the contract. A person can settle it after 14 days.",
  withdrawRequest: "Your request was taken back.",
  settleStalledCase:
    "The milestone was split 50/50 and the escrow has ended.",
  resign: "You left the project. The unpaid money went back to the client.",
};

// Every action here is a transaction the person signs from their own
// wallet; the backend only reads the result afterwards. The checks before
// sending exist to say in plain words why it would fail, not to protect
// anything: the contract refuses whatever is not allowed.
export function useCaseAction(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.caseAction(normalizedProjectId, freelancerId),

    mutationFn: async ({ escrow, action, onChainIndex, expectedCase }: CaseActionVariables) => {
      if (!escrow.contractAddress) {
        throw new Error("The escrow contract has not been deployed yet.");
      }
      const address = escrow.contractAddress as Address;
      const chainId = escrow.chainId;
      const key = `${chainId}:${address.toLowerCase()}:${action}:${onChainIndex ?? "next"}:${expectedCase?.kind ?? ""}:${expectedCase?.openedAt ?? ""}`;
      await runCaseActionWithRecovery({
        key, action, index: onChainIndex, expectedCase,
        read: async (index) => {
          const state = await readCaseChainState(address, chainId, index);
          if (!escrow.clientWalletAddress || !escrow.freelancerWalletAddress ||
            state.client.toLowerCase() !== escrow.clientWalletAddress.toLowerCase() ||
            state.freelancer.toLowerCase() !== escrow.freelancerWalletAddress.toLowerCase()) {
            throw new Error("The contract participants do not match this escrow. Refresh before continuing.");
          }
          return state;
        },
        send: async (before) => {
          const account = getAccount(wagmiConfig);
          const expectedWallet = action === "requestPayment" || action === "resign" ? before.freelancer
            : action === "requestRefund" ? before.client
            : action === "withdrawRequest" ? (before.kind === 1 ? before.freelancer : before.client) : null;
          if (!account.address || account.chainId !== chainId ||
            (expectedWallet ? account.address.toLowerCase() !== expectedWallet.toLowerCase()
              : ![before.client, before.freelancer].some((wallet) => wallet.toLowerCase() === account.address?.toLowerCase()))) {
            throw new Error("Connect the correct participant wallet on this escrow's network before signing.");
          }
          return writeContract(wagmiConfig, { address, abi: escrowAbi, functionName: action, chainId });
        },
        wait: (hash) => waitForTransactionReceipt(wagmiConfig, { hash, chainId }),
        sync: () => syncEscrow(normalizedProjectId, freelancerId),
      });
      return action;
    },

    onSuccess: async (action, { escrow }) => {
      await Promise.all([
        invalidateEscrowWorkflow(queryClient, normalizedProjectId),
        queryClient.invalidateQueries({ queryKey: ["escrow-case-window", escrow.chainId, escrow.contractAddress?.toLowerCase()] }),
      ]);
      toast.success(successMessages[action]);
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "The action did not go through."));
    },
  });
}
