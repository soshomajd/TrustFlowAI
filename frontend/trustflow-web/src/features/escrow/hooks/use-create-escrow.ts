"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { createEscrow } from "@/features/escrow/api/escrow-api";
import { escrowQueryKeys } from "@/features/escrow/queries/escrow-query-keys";
import type { CreateEscrowRequest } from "@/features/escrow/types/escrow";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useCreateEscrow(projectId: string) {
  const queryClient = useQueryClient();

  const normalizedProjectId = projectId.trim();

  return useMutation({
    mutationKey: escrowQueryKeys.create(normalizedProjectId),

    mutationFn: (request: CreateEscrowRequest) =>
      createEscrow(normalizedProjectId, request),

    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: escrowQueryKeys.detail(normalizedProjectId),
      });

      toast.success("Escrow created. It can now be deployed on-chain.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Escrow could not be created."));
    },
  });
}
