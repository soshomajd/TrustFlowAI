"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import type { Address } from "viem";
import {
  getBalance,
  getConnection,
  waitForTransactionReceipt,
  writeContract,
} from "wagmi/actions";

import {
  ACTIVE_CHAIN_ID,
  ACTIVE_CHAIN_NAME,
  ACTIVE_TOKEN_ADDRESS,
  MIN_GAS_BALANCE,
  TEST_TOKEN_MINT_ENABLED,
  isLocalHardhat,
} from "@/features/escrow/lib/network-config";
import { addLocalTestGas } from "@/features/wallet/api/wallet-api";
import { getWalletErrorMessage } from "@/lib/wallet/get-wallet-error-message";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

const mintAbi = [
  {
    type: "function",
    name: "mint",
    stateMutability: "nonpayable",
    inputs: [
      { name: "to", type: "address" },
      { name: "amount", type: "uint256" },
    ],
    outputs: [],
  },
] as const;

type GetTestFundsRequest = {
  tokenAddress: string;
  tokenShortfall: bigint;
  expectedWalletAddress: string;
};

// This action is only for the configured, valueless MockUSDC contract.
// It is disabled unless the environment explicitly enables test minting.
export function useMintTestTokens() {
  return useMutation({
    mutationKey: ["escrow", "mint-test-tokens"],

    mutationFn: async ({
      tokenAddress,
      tokenShortfall,
      expectedWalletAddress,
    }: GetTestFundsRequest) => {
      if (!TEST_TOKEN_MINT_ENABLED) {
        throw new Error("Test token minting is disabled for this environment.");
      }

      if (
        tokenAddress.toLowerCase() !== ACTIVE_TOKEN_ADDRESS.toLowerCase()
      ) {
        throw new Error("The test faucet only supports the configured test token.");
      }

      const connection = getConnection(wagmiConfig);

      if (!connection.address) {
        throw new Error("Connect your wallet first.");
      }

      if (
        connection.address.toLowerCase() !==
        expectedWalletAddress.toLowerCase()
      ) {
        throw new Error("Select the wallet verified for this account.");
      }

      if (connection.chainId !== ACTIVE_CHAIN_ID) {
        throw new Error(`Switch your wallet to ${ACTIVE_CHAIN_NAME} first.`);
      }

      const gasBalance = await getBalance(wagmiConfig, {
        address: connection.address,
        chainId: ACTIVE_CHAIN_ID,
      });

      if (gasBalance.value < MIN_GAS_BALANCE) {
        if (isLocalHardhat) {
          await addLocalTestGas();
        } else {
          throw new Error(
            `Get Sepolia ETH from a faucet before minting test tokens.`,
          );
        }
      }

      if (tokenShortfall <= 0n) {
        return { mintedAmount: 0n };
      }

      const hash = await writeContract(wagmiConfig, {
        address: tokenAddress as Address,
        abi: mintAbi,
        functionName: "mint",
        args: [connection.address, tokenShortfall],
        chainId: ACTIVE_CHAIN_ID,
      });

      await waitForTransactionReceipt(wagmiConfig, {
        hash,
        chainId: ACTIVE_CHAIN_ID,
      });

      return { mintedAmount: tokenShortfall };
    },

    onSuccess: () => {
      toast.success("Test tokens are ready.");
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Minting test tokens failed."));
    },
  });
}
