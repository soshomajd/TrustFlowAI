import "client-only";

import { defineChain } from "viem";
import { createConfig, http, injected } from "wagmi";

import { env } from "@/config/env";

export const configuredChain = defineChain({
  id: env.chainId,
  name: env.chainName,
  nativeCurrency: {
    name: "Ether",
    symbol: "ETH",
    decimals: 18,
  },
  rpcUrls: {
    default: {
      http: [env.rpcUrl],
    },
  },
  blockExplorers: env.blockExplorerUrl
    ? {
        default: {
          name: `${env.chainName} explorer`,
          url: env.blockExplorerUrl,
        },
      }
    : undefined,
  testnet: true,
});

export const wagmiConfig = createConfig({
  chains: [configuredChain],
  connectors: [injected()],
  transports: {
    [configuredChain.id]: http(env.rpcUrl),
  },
  ssr: true,
});
