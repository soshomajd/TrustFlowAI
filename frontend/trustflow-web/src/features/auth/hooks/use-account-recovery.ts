"use client";

import { useMutation } from "@tanstack/react-query";
import axios from "axios";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import {
  forgotPassword,
  resendVerification,
  resetPassword,
  verifyEmail,
} from "@/features/auth/api/auth-api";
import type {
  ResetPasswordRequest,
  VerifyEmailRequest,
} from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import type { ApiProblemDetails } from "@/types/api";

export function useVerifyEmail(admin = false) {
  const router = useRouter();

  return useMutation({
    mutationFn: (request: VerifyEmailRequest) => verifyEmail(request),

    onSuccess: () => {
      toast.success("Email verified. Sign in to continue.");

      router.replace(admin ? "/admin/login" : "/login");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The code could not be checked."));
    },
  });
}

// How long the server makes the user wait before sending another code, read
// from a "resend_throttled" reply. Null for every other kind of failure.
export function getResendWaitSeconds(error: unknown) {
  if (
    axios.isAxiosError<ApiProblemDetails>(error) &&
    error.response?.status === 429 &&
    error.response.data?.code === "resend_throttled"
  ) {
    return error.response.data.retryAfterSeconds ?? 60;
  }

  return null;
}

export function useResendVerification() {
  return useMutation({
    mutationFn: (email: string) => resendVerification(email),

    onSuccess: () => {
      toast.success("A new code is on its way. Check your inbox.");
    },

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "A new code could not be sent."),
      );
    },
  });
}

export function useForgotPassword(admin = false) {
  const router = useRouter();

  return useMutation({
    mutationFn: (email: string) => forgotPassword(email),

    onSuccess: (_response, email) => {
      toast.success("If that address has an account, a code is on its way.");

      router.push(`${admin ? "/admin/reset-password" : "/reset-password"}?email=${encodeURIComponent(email)}`);
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The request could not be sent."));
    },
  });
}

// Asks for another reset code without leaving the reset screen, so what the
// user already typed stays put.
export function useResendResetCode() {
  return useMutation({
    mutationFn: (email: string) => forgotPassword(email),

    onSuccess: () => {
      toast.success("If that address has an account, a new code is on its way.");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The request could not be sent."));
    },
  });
}

export function useResetPassword(admin = false) {
  const router = useRouter();

  return useMutation({
    mutationFn: (request: ResetPasswordRequest) => resetPassword(request),

    onSuccess: () => {
      toast.success("Password changed. Sign in with the new one.");

      router.replace(admin ? "/admin/login" : "/login");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The password was not changed."));
    },
  });
}
