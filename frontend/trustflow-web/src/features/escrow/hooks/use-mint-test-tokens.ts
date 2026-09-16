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
  LOCAL_HARDHAT_CHAIN_ID,
  LOCAL_MOCK_USDC_ADDRESS,
  MIN_LOCAL_GAS_BALANCE,
} from "@/features/escrow/lib/local-test-chain";
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

type GetLocalTestFundsRequest = {
  tokenAddress: string;
  tokenShortfall: bigint;
  expectedWalletAddress: string;
};

// MockUSDC's mint() is public with no access control and is explicitly a
// local/test-only contract, so this only ever targets that dev token -
// it is not something a real ERC20 would ever support.
export function useMintTestTokens() {
  return useMutation({
    mutationKey: ["escrow", "mint-test-tokens"],

    mutationFn: async ({
      tokenAddress,
      tokenShortfall,
      expectedWalletAddress,
    }: GetLocalTestFundsRequest) => {
      if (process.env.NODE_ENV !== "development") {
        throw new Error("The test faucet is only available in local development.");
      }

      if (
        tokenAddress.toLowerCase() !==
        LOCAL_MOCK_USDC_ADDRESS.toLowerCase()
      ) {
        throw new Error("The test faucet only supports the local Mock USDC token.");
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

      if (connection.chainId !== LOCAL_HARDHAT_CHAIN_ID) {
        throw new Error("Switch your wallet to Localhost 31337 first.");
      }

      const gasBalance = await getBalance(wagmiConfig, {
        address: connection.address,
        chainId: LOCAL_HARDHAT_CHAIN_ID,
      });

      if (gasBalance.value < MIN_LOCAL_GAS_BALANCE) {
        await addLocalTestGas();
      }

      if (tokenShortfall <= 0n) {
        return { mintedAmount: 0n };
      }

      const hash = await writeContract(wagmiConfig, {
        address: tokenAddress as Address,
        abi: mintAbi,
        functionName: "mint",
        args: [connection.address, tokenShortfall],
      });

      await waitForTransactionReceipt(wagmiConfig, { hash });

      return { mintedAmount: tokenShortfall };
    },

    onSuccess: () => {
      toast.success("Local test USDC and gas are ready.");
    },

    onError: (error) => {
      toast.error(getWalletErrorMessage(error, "Minting test tokens failed."));
    },
  });
}
