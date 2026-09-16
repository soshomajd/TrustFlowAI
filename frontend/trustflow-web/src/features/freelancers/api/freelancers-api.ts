import { apiClient } from "@/lib/api/api-client";
import type { FreelancerProfile, ResumeMetadata, UpdateFreelancerProfileRequest } from "@/features/freelancers/types/freelancer-profile";

export async function getMyFreelancerProfile(signal?: AbortSignal) {
  return (await apiClient.get<FreelancerProfile>("/freelancers/me/profile", { signal })).data;
}

export async function getFreelancerProfile(userId: string, signal?: AbortSignal) {
  return (await apiClient.get<FreelancerProfile>(`/freelancers/${userId}/profile`, { signal })).data;
}

export async function updateFreelancerProfile(values: UpdateFreelancerProfileRequest) {
  return (await apiClient.put<FreelancerProfile>("/freelancers/me/profile", values)).data;
}

export async function uploadFreelancerResume(file: File) {
  const form = new FormData();
  form.append("file", file);
  return (await apiClient.post<ResumeMetadata>("/freelancers/me/resume", form)).data;
}

export async function deleteFreelancerResume() {
  await apiClient.delete("/freelancers/me/resume");
}

export async function downloadFreelancerResume(userId: string) {
  const response = await apiClient.get<Blob>(`/freelancers/${userId}/resume`, { responseType: "blob" });
  const url = URL.createObjectURL(response.data);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "resume.pdf";
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}
