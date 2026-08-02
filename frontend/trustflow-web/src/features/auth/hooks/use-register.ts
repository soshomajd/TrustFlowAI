"use client";

import { useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import { register } from "@/features/auth/api/auth-api";
import type { RegisterRequest } from "@/features/auth/types/auth";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useRegister() {
  const router = useRouter();

  return useMutation({
    mutationFn: (request: RegisterRequest) => register(request),

    onSuccess: (user) => {
      toast.success(`Account created for ${user.fullName}.`);

      router.replace("/login?registered=true");
    },

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "Account creation failed."));
    },
  });
}
