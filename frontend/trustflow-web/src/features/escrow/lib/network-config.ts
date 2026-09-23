import { env } from "@/config/env";

export const ACTIVE_CHAIN_ID = env.chainId;
export const ACTIVE_CHAIN_NAME = env.chainName;
export const ACTIVE_TOKEN_ADDRESS = env.tokenAddress;
export const ACTIVE_TOKEN_SYMBOL = env.tokenSymbol;
export const BLOCK_EXPLORER_URL = env.blockExplorerUrl;
export const TEST_TOKEN_MINT_ENABLED = env.enableTestTokenMint;

export const MIN_GAS_BALANCE = 1_000_000_000_000_000n;

export const isLocalHardhat = ACTIVE_CHAIN_ID === 31_337;
