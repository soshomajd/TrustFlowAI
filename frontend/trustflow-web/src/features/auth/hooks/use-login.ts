"use client";

import { useMutation } from "@tanstack/react-query";
import axios from "axios";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { login } from "@/features/auth/api/auth-api";
import type { LoginRequest } from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";
import type { ApiProblemDetails } from "@/types/api";

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

    onError: (error, request) => {
      if (
        axios.isAxiosError<ApiProblemDetails>(error) &&
        error.response?.status === 403 &&
        error.response.data?.code === "email_not_verified"
      ) {
        // The password was right; the address still needs confirming.
        toast.info("Verify your email to finish signing in.");

        router.push(
          `/verify-email?email=${encodeURIComponent(
            error.response.data.email ?? request.email,
          )}`,
        );

        return;
      }

      toast.error(
        getApiErrorMessage(error, "Login failed. Check your credentials."),
      );
    },
  });
}
