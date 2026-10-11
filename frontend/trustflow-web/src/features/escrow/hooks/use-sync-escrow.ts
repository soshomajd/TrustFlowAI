"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useSyncEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.sync(normalizedProjectId, freelancerId),

    mutationFn: () => syncEscrow(normalizedProjectId, freelancerId),

    onSuccess: async (escrow) => {
      await queryClient.invalidateQueries({
        queryKey: escrowQueryKeys.participants(normalizedProjectId),
      });

      if (escrow.status === "Funded") {
        toast.success("Escrow funding confirmed on-chain.");
      }
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Escrow status could not be refreshed."),
      );
    },
  });
}
