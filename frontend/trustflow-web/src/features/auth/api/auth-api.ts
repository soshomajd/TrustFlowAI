import "client-only";

import type {
  AuthSessionResponse,
  LoginRequest,
  MessageResponse,
  RegisterRequest,
  RegisterResponse,
  ResetPasswordRequest,
  VerifyEmailRequest,
} from "@/features/auth/types/auth";
import { apiClient } from "@/lib/api/api-client";

export async function login(request: LoginRequest) {
  const response = await apiClient.post<AuthSessionResponse>(
    "/auth/login",
    request,
  );

  return response.data;
}

export async function register(request: RegisterRequest) {
  const response = await apiClient.post<RegisterResponse>(
    "/auth/register",
    request,
  );

  return response.data;
}

export async function verifyEmail(request: VerifyEmailRequest) {
  const response = await apiClient.post<MessageResponse>(
    "/auth/verify-email",
    request,
  );

  return response.data;
}

export async function resendVerification(email: string) {
  const response = await apiClient.post<MessageResponse>(
    "/auth/resend-verification",
    { email },
  );

  return response.data;
}

export async function forgotPassword(email: string) {
  const response = await apiClient.post<MessageResponse>(
    "/auth/forgot-password",
    { email },
  );

  return response.data;
}

export async function resetPassword(request: ResetPasswordRequest) {
  const response = await apiClient.post<MessageResponse>(
    "/auth/reset-password",
    request,
  );

  return response.data;
}

export async function logout() {
  await apiClient.post("/auth/logout");
}

export async function logoutAll() {
  await apiClient.post("/auth/logout-all");
}
