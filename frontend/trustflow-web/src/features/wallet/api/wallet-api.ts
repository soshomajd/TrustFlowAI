import "client-only";

import type {
  WalletChallengeResponse,
  WalletStatusResponse,
  WalletVerificationResponse,
} from "@/features/wallet/types/wallet";
import { apiClient } from "@/lib/api/api-client";

export async function getMyWallet(signal?: AbortSignal) {
  const response = await apiClient.get<WalletStatusResponse>("/wallet/me", {
    signal,
  });

  return response.data;
}

export async function addLocalTestGas() {
  await apiClient.post("/wallet/local-test-gas");
}

export async function createWalletChallenge(walletAddress: string) {
  const response = await apiClient.post<WalletChallengeResponse>(
    "/wallet/challenge",
    { walletAddress },
  );

  return response.data;
}

export async function verifyWallet(signature: string) {
  const response = await apiClient.post<WalletVerificationResponse>(
    "/wallet/verify",
    { signature },
  );

  return response.data;
}
