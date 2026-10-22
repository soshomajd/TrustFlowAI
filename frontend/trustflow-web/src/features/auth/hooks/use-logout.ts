"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { logout } from "@/features/auth/api/auth-api";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";
import { isAdminDashboardPath } from "@/features/auth/lib/sign-in-path";
import { usePathname } from "next/navigation";

export function useLogout() {
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();

  const clearSession = useAuthStore((state) => state.clearSession);

  return useMutation({
    mutationFn: logout,

    onSuccess: () => {
      clearSession();

      queryClient.clear();

      toast.success("You have been signed out.");

      router.replace(isAdminDashboardPath(pathname) ? "/admin/login" : "/login");
      router.refresh();
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Logout failed. Please try again."),
      );
    },
  });
}
