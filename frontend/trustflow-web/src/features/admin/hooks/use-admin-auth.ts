"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import axios from "axios";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { adminLogin, setupAdmin, verifyAdminMfa } from "@/features/admin/api/admin-auth-api";
import { getAdminReturnPath } from "@/features/auth/lib/sign-in-path";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";
import type { ApiProblemDetails } from "@/types/api";

export function useAdminLogin() {
  const router = useRouter();
  return useMutation({
    mutationFn: adminLogin,
    onError: (error, request) => {
      if (axios.isAxiosError<ApiProblemDetails>(error) && error.response?.status === 403 && error.response.data?.code === "email_not_verified") {
        router.push(`/admin/verify-email?email=${encodeURIComponent(request.email)}`);
        return;
      }
      toast.error(getApiErrorMessage(error, "Administrator sign-in failed."));
    },
  });
}

export function useAdminMfa(next?: string) {
  const router = useRouter();
  const setSession = useAuthStore((state) => state.setSession);
  const cache = useQueryClient();
  return useMutation({
    mutationFn: verifyAdminMfa,
    onSuccess: (session) => {
      cache.clear();
      setSession(session);
      router.replace(getAdminReturnPath(next));
      router.refresh();
    },
  });
}

export function useAdminSetup() {
  const router = useRouter();
  return useMutation({
    mutationFn: setupAdmin,
    onSuccess: (account) => {
      router.replace(`/admin/verify-email?email=${encodeURIComponent(account.email)}&sent=${account.verificationEmailSent}`);
    },
    onError: (error) => toast.error(getApiErrorMessage(error, "Administrator setup failed.")),
  });
}
