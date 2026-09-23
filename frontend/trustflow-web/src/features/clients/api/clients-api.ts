import "client-only";

import { apiClient } from "@/lib/api/api-client";
import type { ClientProfile, UpdateClientProfileRequest } from "@/features/clients/types/client-profile";

export async function getMyClientProfile(signal?: AbortSignal) {
  return (await apiClient.get<ClientProfile>("/clients/me/profile", { signal })).data;
}

export async function getClientProfile(clientId: string, signal?: AbortSignal) {
  return (await apiClient.get<ClientProfile>(`/clients/${clientId}/profile`, { signal })).data;
}

export async function updateClientProfile(values: UpdateClientProfileRequest) {
  return (await apiClient.put<ClientProfile>("/clients/me/profile", values)).data;
}

