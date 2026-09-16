import { isAxiosError } from "axios";

import { getApiErrorMessage } from "@/lib/api/get-api-error-message";

type ViemLikeError = {
  shortMessage?: string;
};

export function getWalletErrorMessage(
  error: unknown,
  fallbackMessage = "The wallet action could not be completed.",
) {
  if (isAxiosError(error)) {
    return getApiErrorMessage(error, fallbackMessage);
  }

  const shortMessage = (error as ViemLikeError | undefined)?.shortMessage;

  if (shortMessage) {
    return shortMessage;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}
