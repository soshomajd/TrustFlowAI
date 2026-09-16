"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { isAxiosError } from "axios";
import { toast } from "sonner";
import { injected } from "wagmi";
import { connect, getConnection, signMessage } from "wagmi/actions";

import {
  createWalletChallenge,
  getMyWallet,
  verifyWallet,
} from "@/features/wallet/api/wallet-api";
import { walletQueryKeys } from "@/features/wallet/queries/wallet-query-keys";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

function isAlreadyConnectedToSelf(error: unknown) {
  return (
    isAxiosError<{ message?: string }>(error) &&
    error.response?.status === 409 &&
    error.response.data?.message
      ?.toLowerCase()
      .includes("already connected to your account")
  );
}

export function useConnectAndVerifyWallet() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationKey: walletQueryKeys.connect(),

    mutationFn: async () => {
      const walletStatus = await getMyWallet();

      let connection = getConnection(wagmiConfig);

      if (!connection.isConnected) {
        await connect(wagmiConfig, { connector: injected() });

        connection = getConnection(wagmiConfig);
      }

      const address = connection.address;

      if (!address) {
        throw new Error("No wallet address available after connecting.");
      }

      if (walletStatus.isVerified && walletStatus.walletAddress) {
        if (
          walletStatus.walletAddress.toLowerCase() !== address.toLowerCase()
        ) {
          throw new Error(
            `Switch your wallet account to ${walletStatus.walletAddress} and try again.`,
          );
        }

        return walletStatus.walletAddress;
      }

      try {
        const challenge = await createWalletChallenge(address);

        const signature = await signMessage(wagmiConfig, {
          account: address,
          message: challenge.message,
        });

        await verifyWallet(signature);
      } catch (error) {
        if (!isAlreadyConnectedToSelf(error)) {
          throw error;
        }
      }

      return address;
    },

    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: walletQueryKeys.current(),
      });

      toast.success("Wallet connected and verified.");
    },

    onError: (error) => {
      toast.error(
        getWalletErrorMessage(error, "Wallet could not be connected."),
      );
    },
  });
}
