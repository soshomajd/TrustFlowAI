import axios from "axios";

import type { ApiProblemDetails } from "@/types/api";

export function getApiErrorMessage(
  error: unknown,
  fallbackMessage = "Something went wrong. Please try again.",
) {
  if (!axios.isAxiosError<ApiProblemDetails>(error)) {
    return fallbackMessage;
  }

  const responseData = error.response?.data;

  if (responseData?.message) {
    return responseData.message;
  }

  if (responseData?.detail) {
    return responseData.detail;
  }

  if (responseData?.title) {
    return responseData.title;
  }

  const firstValidationError = responseData?.errors
    ? Object.values(responseData.errors).flat()[0]
    : undefined;

  return firstValidationError ?? fallbackMessage;
}
