"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { logout } from "@/features/auth/api/auth-api";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";

export function useLogout() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const clearSession = useAuthStore((state) => state.clearSession);

  return useMutation({
    mutationFn: logout,

    onSuccess: () => {
      clearSession();

      queryClient.clear();

      toast.success("You have been signed out.");

      router.replace("/login");
      router.refresh();
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Logout failed. Please try again."),
      );
    },
  });
}
