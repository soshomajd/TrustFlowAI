"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import {
  readContract,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import type { ProjectEscrow } from "@/features/escrow/types/escrow";
import { milestoneQueryKeys } from "@/features/milestones/queries/milestone-query-keys";
import { notificationQueryKeys } from "@/features/notifications/queries/notification-query-keys";
import { projectQueryKeys } from "@/features/projects/queries/project-query-keys";
import { proposalQueryKeys } from "@/features/proposals/queries/proposal-query-keys";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export type CaseAction =
  | "requestPayment"
  | "requestRefund"
  | "withdrawRequest"
  | "settleStalledCase"
  | "resign";

export type CaseActionVariables = {
  escrow: ProjectEscrow;
  action: CaseAction;

  // The position of the milestone among this freelancer's stages. A request
  // always concerns the next unpaid one.
  onChainIndex?: number;
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

    mutationFn: async ({ escrow, action, onChainIndex }: CaseActionVariables) => {
      if (!escrow.contractAddress) {
        throw new Error("The escrow contract has not been deployed yet.");
      }

      const address = escrow.contractAddress as Address;
      const chainId = escrow.chainId;

      const workStarted = await readContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: "workStarted",
        chainId,
      });

      if (!workStarted) {
        throw new Error("Work has not started on the contract yet.");
      }

      if (action === "requestPayment" || action === "requestRefund") {
        const [openKind, nextIndex] = await Promise.all([
          readContract(wagmiConfig, {
            address,
            abi: escrowAbi,
            functionName: "getOpenCase",
            chainId,
          }),
          readContract(wagmiConfig, {
            address,
            abi: escrowAbi,
            functionName: "nextMilestoneIndex",
            chainId,
          }),
        ]);

        if (openKind[0] !== 0) {
          throw new Error("A request is already open on this escrow.");
        }

        if (
          onChainIndex !== undefined &&
          BigInt(onChainIndex) !== nextIndex
        ) {
          throw new Error(
            "A request always concerns the next unpaid milestone. Finish the earlier ones first.",
          );
        }
      }

      const hash = await writeContract(wagmiConfig, {
        address,
        abi: escrowAbi,
        functionName: action,
        chainId,
      });

      await waitForTransactionReceipt(wagmiConfig, { hash, chainId });

      // The chain is the truth. Reading it back is what makes the app show
      // the change.
      await syncEscrow(normalizedProjectId, freelancerId);

      return action;
    },

    onSuccess: async (action) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: escrowQueryKeys.cases(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: escrowQueryKeys.participants(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: notificationQueryKeys.all,
        }),

        // Ending an escrow early hands stages back and rejects a proposal.
        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.projectLists(),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.assignedLists(),
        }),
      ]);

      toast.success(successMessages[action]);
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "The action did not go through."));
    },
  });
}
