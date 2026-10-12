"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";

import {
  coachProfile,
  draftProject,
  draftResume,
} from "@/features/ai/api/ai-api";
import { aiQueryKeys } from "@/features/ai/queries/ai-query-keys";
import type {
  ProjectDraftRequest,
  ResumeDraftRequest,
} from "@/features/ai/types/ai";
import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

export function useProjectDraft() {
  return useMutation({
    mutationKey: aiQueryKeys.draftProject(),

    mutationFn: (request: ProjectDraftRequest) => draftProject(request),

    onError: (error) => {
      toast.error(getApiErrorMessage(error, "The AI draft could not be made."));
    },
  });
}

export function useProfileCoach() {
  return useMutation({
    mutationKey: aiQueryKeys.coachProfile(),

    mutationFn: () => coachProfile(),

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "The AI profile review could not be made."),
      );
    },
  });
}

export function useResumeDraft() {
  return useMutation({
    mutationKey: aiQueryKeys.draftResume(),

    mutationFn: (request: ResumeDraftRequest) => draftResume(request),

    onError: (error) => {
      toast.error(
        getApiErrorMessage(error, "The AI resume draft could not be made."),
      );
    },
  });
}
