"use client";

import { useMutation } from "@tanstack/react-query";
import axios from "axios";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { register } from "@/features/auth/api/auth-api";
import type { RegisterRequest } from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";
import type { ApiProblemDetails } from "@/types/api";

export function useRegister() {
  const router = useRouter();

  return useMutation({
    mutationFn: (request: RegisterRequest) => register(request),

    onSuccess: (user) => {
      toast.success(
        user.verificationEmailSent
          ? `Account created. We emailed a code to ${user.email}.`
          : "Account created. Ask for a verification code to continue.",
      );

      router.replace(
        `/verify-email?email=${encodeURIComponent(user.email)}` +
          `&sent=${user.verificationEmailSent}`,
      );
    },

    onError: (error) => {
      if (axios.isAxiosError<ApiProblemDetails>(error) && error.response?.status === 403 && error.response.data?.code === "admin_setup_required") {
        toast.info("Use the approved administrator activation page.");
        router.replace("/admin/setup");
        return;
      }
      toast.error(getApiErrorMessage(error, "Account creation failed."));
    },
  });
}
