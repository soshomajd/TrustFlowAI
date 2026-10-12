import "client-only";

import type {
  ReviewCaseDetail,
  ReviewCaseSummary,
} from "@/features/review/types/review";
import { apiClient } from "@/lib/api/api-client";

// A model call can take much longer than an ordinary request.
const AI_TIMEOUT = { timeout: 120_000 } as const;

export async function listReviewCases(status: "open" | "closed") {
  const response = await apiClient.get<ReviewCaseSummary[]>("/review/cases", {
    params: { status },
  });

  return response.data;
}

export async function getReviewCase(caseId: string) {
  const response = await apiClient.get<ReviewCaseDetail>(
    `/review/cases/${caseId}`,
  );

  return response.data;
}

// Reads the contract again and records what it says. It changes nothing on
// the chain.
export async function syncReviewCase(caseId: string) {
  const response = await apiClient.post<ReviewCaseDetail>(
    `/review/cases/${caseId}/sync`,
  );

  return response.data;
}

export async function adviseReviewCase(caseId: string) {
  const response = await apiClient.post<ReviewCaseDetail>(
    `/review/cases/${caseId}/advice`,
    undefined,
    AI_TIMEOUT,
  );

  return response.data;
}

export async function saveReviewNote(caseId: string, note: string) {
  const response = await apiClient.put<ReviewCaseDetail>(
    `/review/cases/${caseId}/note`,
    { note },
  );

  return response.data;
}

export async function downloadReviewFile(caseId: string, attachmentId: string) {
  const response = await apiClient.get<Blob>(
    `/review/cases/${caseId}/files/${attachmentId}`,
    { responseType: "blob" },
  );

  return response.data;
}
