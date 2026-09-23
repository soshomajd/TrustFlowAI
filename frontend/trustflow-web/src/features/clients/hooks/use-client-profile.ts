"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { getClientProfile, getMyClientProfile, updateClientProfile } from "@/features/clients/api/clients-api";
import { clientQueryKeys } from "@/features/clients/queries/client-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";
import { useAuthStore } from "@/stores/auth-store";

export function useMyClientProfile() {
  const user = useAuthStore((state) => state.user);
  return useQuery({
    queryKey: clientQueryKeys.mine(user?.id ?? ""), queryFn: ({ signal }) => getMyClientProfile(signal),
    enabled: Boolean(user?.roles.includes("Client")), staleTime: 30_000, refetchOnWindowFocus: false, retry: 1,
  });
}

export function useClientProfile(userId: string) {
  return useQuery({
    queryKey: clientQueryKeys.detail(userId), queryFn: ({ signal }) => getClientProfile(userId, signal),
    enabled: isValidGuid(userId), staleTime: 30_000, retry: 1,
  });
}

function useSetClientProfile() {
  const queryClient = useQueryClient();
  return (profile: Awaited<ReturnType<typeof getMyClientProfile>>) => {
    queryClient.setQueryData(clientQueryKeys.mine(profile.userId), profile);
    queryClient.setQueryData(clientQueryKeys.detail(profile.userId), profile);
  };
}

export function useUpdateClientProfile() {
  const setProfile = useSetClientProfile();
  return useMutation({ mutationFn: updateClientProfile, onSuccess: (profile) => {
    setProfile(profile); toast.success("Your client profile has been saved.");
  }});
}

