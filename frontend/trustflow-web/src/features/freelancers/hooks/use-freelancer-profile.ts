"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { deleteFreelancerResume, getFreelancerProfile, getMyFreelancerProfile, updateFreelancerProfile, uploadFreelancerResume } from "@/features/freelancers/api/freelancers-api";
import { freelancerQueryKeys } from "@/features/freelancers/queries/freelancer-query-keys";
import { isValidGuid } from "@/lib/validation/is-valid-guid";
import { useAuthStore } from "@/stores/auth-store";
import type { FreelancerProfile, ResumeMetadata } from "@/features/freelancers/types/freelancer-profile";

export function useMyFreelancerProfile() {
  const user = useAuthStore((state) => state.user);
  return useQuery({
    queryKey: freelancerQueryKeys.mine(user?.id ?? ""),
    queryFn: ({ signal }) => getMyFreelancerProfile(signal),
    enabled: Boolean(user?.roles.includes("Freelancer")),
    staleTime: 30_000,
    refetchOnWindowFocus: false,
    retry: 1,
  });
}

function mergeResume(profile: FreelancerProfile | undefined, metadata: ResumeMetadata) {
  return profile ? { ...profile, ...metadata } : profile;
}

export function useUploadFreelancerResume() {
  const queryClient = useQueryClient();
  const userId = useAuthStore((state) => state.user?.id ?? "");
  return useMutation({
    mutationFn: uploadFreelancerResume,
    onSuccess: (metadata) => {
      queryClient.setQueryData<FreelancerProfile>(freelancerQueryKeys.mine(userId), (profile) => mergeResume(profile, metadata));
      queryClient.setQueryData<FreelancerProfile>(freelancerQueryKeys.detail(userId), (profile) => mergeResume(profile, metadata));
      toast.success("Your resume has been uploaded.");
    },
  });
}

export function useDeleteFreelancerResume() {
  const queryClient = useQueryClient();
  const userId = useAuthStore((state) => state.user?.id ?? "");
  return useMutation({
    mutationFn: deleteFreelancerResume,
    onSuccess: () => {
      const metadata = { hasResume: false, resumeFileSize: null, resumeUploadedAt: null };
      queryClient.setQueryData<FreelancerProfile>(freelancerQueryKeys.mine(userId), (profile) => mergeResume(profile, metadata));
      queryClient.setQueryData<FreelancerProfile>(freelancerQueryKeys.detail(userId), (profile) => mergeResume(profile, metadata));
      toast.success("Your resume has been removed.");
    },
  });
}

export function useFreelancerProfile(userId: string) {
  return useQuery({
    queryKey: freelancerQueryKeys.detail(userId),
    queryFn: ({ signal }) => getFreelancerProfile(userId, signal),
    enabled: isValidGuid(userId),
    staleTime: 30_000,
    retry: 1,
  });
}

export function useUpdateFreelancerProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateFreelancerProfile,
    onSuccess: (profile) => {
      queryClient.setQueryData(freelancerQueryKeys.mine(profile.userId), profile);
      queryClient.setQueryData(freelancerQueryKeys.detail(profile.userId), profile);
      toast.success("Your profile has been saved.");
    },
  });
}
