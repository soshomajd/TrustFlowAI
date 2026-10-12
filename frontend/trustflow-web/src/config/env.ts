import * as z from "zod";

const clientEnvironmentSchema = z.object({
  NEXT_PUBLIC_API_URL: z.url(),
  NEXT_PUBLIC_CHAIN_ID: z.coerce.number().int().positive().default(31_337),
  NEXT_PUBLIC_CHAIN_NAME: z.string().trim().min(1).default("Localhost"),
  NEXT_PUBLIC_RPC_URL: z.url().default("http://127.0.0.1:8545"),
  NEXT_PUBLIC_TOKEN_ADDRESS: z
    .string()
    .regex(/^0x[0-9a-fA-F]{40}$/)
    .default("0x5FbDB2315678afecb367f032d93F642f64180aa3"),
  NEXT_PUBLIC_TOKEN_SYMBOL: z.string().trim().min(1).default("mUSDC"),
  NEXT_PUBLIC_BLOCK_EXPLORER_URL: z.url().optional(),
  NEXT_PUBLIC_ENABLE_TEST_TOKEN_MINT: z
    .enum(["true", "false"])
    .default("false")
    .transform((value) => value === "true"),
});

const parsedEnvironment = clientEnvironmentSchema.safeParse({
  NEXT_PUBLIC_API_URL: process.env.NEXT_PUBLIC_API_URL,
  NEXT_PUBLIC_CHAIN_ID: process.env.NEXT_PUBLIC_CHAIN_ID,
  NEXT_PUBLIC_CHAIN_NAME: process.env.NEXT_PUBLIC_CHAIN_NAME,
  NEXT_PUBLIC_RPC_URL: process.env.NEXT_PUBLIC_RPC_URL,
  NEXT_PUBLIC_TOKEN_ADDRESS: process.env.NEXT_PUBLIC_TOKEN_ADDRESS,
  NEXT_PUBLIC_TOKEN_SYMBOL: process.env.NEXT_PUBLIC_TOKEN_SYMBOL,
  NEXT_PUBLIC_BLOCK_EXPLORER_URL:
    process.env.NEXT_PUBLIC_BLOCK_EXPLORER_URL || undefined,
  NEXT_PUBLIC_ENABLE_TEST_TOKEN_MINT:
    process.env.NEXT_PUBLIC_ENABLE_TEST_TOKEN_MINT ??
    (process.env.NODE_ENV === "development" ? "true" : "false"),
});

if (!parsedEnvironment.success) {
  console.error(
    "Invalid frontend environment variables:",
    z.flattenError(parsedEnvironment.error).fieldErrors,
  );

  throw new Error("Frontend environment configuration is invalid.");
}

export const env = {
  apiUrl: parsedEnvironment.data.NEXT_PUBLIC_API_URL.replace(/\/+$/, ""),
  chainId: parsedEnvironment.data.NEXT_PUBLIC_CHAIN_ID,
  chainName: parsedEnvironment.data.NEXT_PUBLIC_CHAIN_NAME,
  rpcUrl: parsedEnvironment.data.NEXT_PUBLIC_RPC_URL,
  tokenAddress: parsedEnvironment.data.NEXT_PUBLIC_TOKEN_ADDRESS,
  tokenSymbol: parsedEnvironment.data.NEXT_PUBLIC_TOKEN_SYMBOL,
  blockExplorerUrl:
    parsedEnvironment.data.NEXT_PUBLIC_BLOCK_EXPLORER_URL?.replace(/\/+$/, ""),
  enableTestTokenMint:
    parsedEnvironment.data.NEXT_PUBLIC_ENABLE_TEST_TOKEN_MINT,
} as const;
