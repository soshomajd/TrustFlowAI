import "client-only";
import { apiClient } from "@/lib/api/api-client";
import type { AuthSessionResponse, LoginRequest, RegisterRequest, RegisterResponse } from "@/features/auth/types/auth";

export type AdminSetupRequest = Omit<RegisterRequest, "role">;
export type AdminSetupResponse = Omit<RegisterResponse, "role"> & { role: "Admin" };

export type AdminMfaPrompt = { mfaRequired: true; challengeToken: string; maskedEmail: string; expiresAt: string };
export async function enterLocalAdminPreview() {
  return (await apiClient.post<AuthSessionResponse>("/auth/admin/local-preview")).data;
}
export async function verifyAdminMfa(request: { challengeToken: string; code: string }) {
  return (await apiClient.post<AuthSessionResponse>("/auth/admin/mfa", request)).data;
}

export async function adminLogin(request: LoginRequest) {
  const response = await apiClient.post<AdminMfaPrompt>("/auth/admin/login", request);
  return response.data;
}

export async function setupAdmin(request: AdminSetupRequest) {
  const response = await apiClient.post<AdminSetupResponse>("/auth/admin/setup", request);
  return response.data;
}
