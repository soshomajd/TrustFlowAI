"use client";

import { useMutation } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { googleSignIn } from "@/features/auth/api/auth-api";
import type {
  GoogleRoleRequiredResponse,
  GoogleSignInRequest,
} from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import { useAuthStore } from "@/stores/auth-store";

// The API answers 409 role_required the first time a Google account is seen,
// because an account cannot exist without being a Client or a Freelancer.
export function isRoleRequiredError(error: unknown) {
  return (
    isAxiosError<GoogleRoleRequiredResponse>(error) &&
    error.response?.status === 409 &&
    error.response.data?.code === "role_required"
  );
}

export function useGoogleSignIn() {
  const router = useRouter();

  const setSession = useAuthStore((state) => state.setSession);

  return useMutation({
    mutationFn: (request: GoogleSignInRequest) => googleSignIn(request),

    onSuccess: (session) => {
      setSession(session);

      toast.success(`Welcome, ${session.user.fullName}.`);

      router.replace("/dashboard");
      router.refresh();
    },

    onError: (error) => {
      // Not a failure: the caller shows a role picker and tries again.
      if (isRoleRequiredError(error)) {
        return;
      }

      toast.error(getApiErrorMessage(error, "Google sign-in failed."));
    },
  });
}
