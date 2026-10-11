"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { deployEscrow } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useDeployEscrow(projectId: string, freelancerId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.deploy(normalizedProjectId, freelancerId),

    mutationFn: () => deployEscrow(normalizedProjectId, freelancerId),

    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: escrowQueryKeys.participants(normalizedProjectId),
      });

      toast.success("Escrow contract deployed on-chain.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Escrow could not be deployed."));
    },
  });
}
