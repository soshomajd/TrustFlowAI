import "client-only";

import { hardhat } from "viem/chains";
import { createConfig, http, injected } from "wagmi";

export const wagmiConfig = createConfig({
  chains: [hardhat],
  connectors: [injected()],
  transports: {
    [hardhat.id]: http(),
  },
  ssr: true,
});
