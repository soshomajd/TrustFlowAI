import "client-only";

import { apiClient } from "@/lib/api/api-client";
import type { ClientProfile, ClientVerificationAdminItem, UpdateClientProfileRequest } from "@/features/clients/types/client-profile";

export async function getMyClientProfile(signal?: AbortSignal) {
  return (await apiClient.get<ClientProfile>("/clients/me/profile", { signal })).data;
}

export async function getClientProfile(clientId: string, signal?: AbortSignal) {
  return (await apiClient.get<ClientProfile>(`/clients/${clientId}/profile`, { signal })).data;
}

export async function updateClientProfile(values: UpdateClientProfileRequest) {
  return (await apiClient.put<ClientProfile>("/clients/me/profile", values)).data;
}

export async function submitClientVerification(input: {
  legalName: string; countryCode: string; registrationNumber: string; file: File;
}) {
  const form = new FormData();
  form.append("legalName", input.legalName);
  form.append("countryCode", input.countryCode);
  if (input.registrationNumber) form.append("registrationNumber", input.registrationNumber);
  form.append("file", input.file);
  return (await apiClient.post<ClientProfile>("/clients/me/verification", form)).data;
}

export async function resetClientVerification() {
  await apiClient.delete("/clients/me/verification");
}

export async function getPendingClientVerifications(signal?: AbortSignal) {
  return (await apiClient.get<ClientVerificationAdminItem[]>("/admin/client-verifications", { signal })).data;
}

export async function reviewClientVerification(input: { clientId: string; approve: boolean; reason: string }) {
  await apiClient.post(`/admin/client-verifications/${input.clientId}/decision`, {
    approve: input.approve, reason: input.reason,
  });
}

export async function downloadClientVerificationDocument(clientId: string) {
  const response = await apiClient.get<Blob>(`/admin/client-verifications/${clientId}/document`, { responseType: "blob" });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "identity-document.pdf";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
