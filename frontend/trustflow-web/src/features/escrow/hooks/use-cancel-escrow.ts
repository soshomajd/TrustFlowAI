"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import { waitForTransactionReceipt, writeContract } from "wagmi/actions";

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

export function useCancelEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();
  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.cancel(normalizedProjectId, freelancerId),

    mutationFn: async (escrow: ProjectEscrow) => {
      if (!escrow.contractAddress) {
        throw new Error("The escrow contract has not been deployed yet.");
      }

      const hash = await writeContract(wagmiConfig, {
        address: escrow.contractAddress as Address,
        abi: escrowAbi,
        functionName: "cancelBeforeWorkStarts",
        chainId: escrow.chainId,
      });

      const receipt = await waitForTransactionReceipt(wagmiConfig, {
        hash,
        chainId: escrow.chainId,
      });

      if (receipt.status !== "success") {
        throw new Error("The cancellation transaction failed. The escrow was not cancelled.");
      }

      const synced = await syncEscrow(normalizedProjectId, freelancerId);
      if (synced.status !== "Cancelled") {
        throw new Error("Cancellation is not yet confirmed by the API. Refresh the escrow status before trying again.");
      }
      return synced;
    },

    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: escrowQueryKeys.participants(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.workspace(normalizedProjectId),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientLists(),
        }),
        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.assignedLists(),
        }),

        // Cancelling hands this freelancer's stages back, so both the
        // stage list and their now-rejected proposal are out of date.
        queryClient.invalidateQueries({
          queryKey: milestoneQueryKeys.projectList(normalizedProjectId),
        }),

        queryClient.invalidateQueries({
          queryKey: proposalQueryKeys.projectLists(),
        }),

        queryClient.invalidateQueries({
          queryKey: projectQueryKeys.clientDashboardSummary(),
        }),
        queryClient.invalidateQueries({ queryKey: notificationQueryKeys.all }),
      ]);

      toast.success(
        "Escrow cancelled. The freelancer has a notification, and those stages are open for hire again.",
      );
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Escrow cancellation failed."));
    },
  });
}
