"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { disconnect } from "wagmi/actions";

import { walletQueryKeys } from "@/features/wallet/queries/wallet-query-keys";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

// This only ends the browser wallet's connection to this site. The address the
// backend verified stays linked to the account, so reconnecting later needs no
// new signature, and it never touches funds or a deployed escrow.
export function useDisconnectWallet() {
  return useMutation({
    mutationKey: walletQueryKeys.disconnect(),

    mutationFn: async () => {
      await disconnect(wagmiConfig);
    },

    onSuccess: () => {
      toast.success(
        "Wallet disconnected. Your verified address stays on your account.",
      );
    },

    onError: (error) => {
      toast.error(
        getWalletErrorMessage(error, "Wallet could not be disconnected."),
      );
    },
  });
}
