"use client";

import { useQuery } from "@tanstack/react-query";

import { getMyWallet } from "@/features/wallet/api/wallet-api";
import { walletQueryKeys } from "@/features/wallet/queries/wallet-query-keys";
import { useAuthStore } from "@/stores/auth-store";

export function useMyWallet() {
  const isAuthenticated = useAuthStore(
    (state) => state.status === "authenticated" && Boolean(state.accessToken),
  );

  return useQuery({
    queryKey: walletQueryKeys.current(),
    queryFn: ({ signal }) => getMyWallet(signal),
    enabled: isAuthenticated,
    staleTime: 30_000,
  });
}
