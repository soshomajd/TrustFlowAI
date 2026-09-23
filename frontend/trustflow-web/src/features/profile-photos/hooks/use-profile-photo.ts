"use client";
import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api/api-client";
import { useAuthStore } from "@/stores/auth-store";
export type ProfilePhoto = { image: string | null };
export const photoKey = (viewer: string, userId: string) => ["profile-photo", viewer, userId] as const;
export function useProfilePhoto(userId?: string | null) {
  const viewer = useAuthStore((state) => state.user?.id ?? "");
  return useQuery({ queryKey: photoKey(viewer, userId ?? ""),
    queryFn: async ({ signal }) => (await apiClient.get<ProfilePhoto>(`/users/${userId}/photo`, { signal })).data,
    enabled: Boolean(viewer && userId), staleTime: 60_000, retry: 1 });
}
