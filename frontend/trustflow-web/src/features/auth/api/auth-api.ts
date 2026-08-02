import "client-only";

import type {
  AuthSessionResponse,
  LoginRequest,
  RegisterRequest,
  RegisterResponse,
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

export async function logout() {
  await apiClient.post("/auth/logout");
}

export async function logoutAll() {
  await apiClient.post("/auth/logout-all");
}
