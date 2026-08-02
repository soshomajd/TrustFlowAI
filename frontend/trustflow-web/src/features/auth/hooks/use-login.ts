"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { login } from "@/features/auth/api/auth-api";
import type { LoginRequest } from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";

export function useLogin() {
  const router = useRouter();

  const setSession = useAuthStore((state) => state.setSession);

  return useMutation({
    mutationFn: (request: LoginRequest) => login(request),

    onSuccess: (session) => {
      setSession(session);

      toast.success(`Welcome back, ${session.user.fullName}.`);

      router.replace("/dashboard");
      router.refresh();
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "Login failed. Check your credentials."),
      );
    },
  });
}
