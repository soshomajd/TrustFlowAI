import { createPublicClient, formatUnits, http, isAddress, parseAbi } from "viem";

const RPC_URL = process.env.TRUSTFLOW_RPC_URL ?? "http://127.0.0.1:8545";

const ESCROW_ADDRESS = process.argv[2] ?? process.env.TRUSTFLOW_ESCROW_ADDRESS;
if (!ESCROW_ADDRESS || !isAddress(ESCROW_ADDRESS)) {
  console.error("Usage: node scripts/readLocalEscrow.mjs <escrow-address>");
  process.exit(1);
}

const escrowAbi = parseAbi([
  "function token() view returns (address)",
  "function client() view returns (address)",
  "function freelancer() view returns (address)",
  "function totalAmount() view returns (uint256)",
  "function releasedAmount() view returns (uint256)",
  "function nextMilestoneIndex() view returns (uint256)",
  "function workStarted() view returns (bool)",
  "function escrowState() view returns (uint8)",
  "function getMilestoneCount() view returns (uint256)",
  "function getMilestone(uint256 milestoneIndex) view returns (uint256 amount, bool released)",
  "function remainingAmount() view returns (uint256)",
]);

const publicClient = createPublicClient({
  transport: http(RPC_URL),
});

const code = await publicClient.getCode({ address: ESCROW_ADDRESS });
if (!code || code === "0x") {
  console.error("No contract exists at this address on the selected RPC. A local node restart removes old deployments.");
  process.exit(1);
}

const [
  token,
  client,
  freelancer,
  totalAmount,
  releasedAmount,
  nextMilestoneIndex,
  workStarted,
  escrowState,
  milestoneCount,
  remainingAmount,
] = await Promise.all([
  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "token",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "client",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "freelancer",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "totalAmount",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "releasedAmount",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "nextMilestoneIndex",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "workStarted",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "escrowState",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "getMilestoneCount",
  }),

  publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "remainingAmount",
  }),
]);

const tokenDecimals = await publicClient.readContract({
  address: token,
  abi: parseAbi(["function decimals() view returns (uint8)"]),
  functionName: "decimals",
});

console.log("Token:", token);
console.log("Client:", client);
console.log("Freelancer:", freelancer);

console.log("Total:", formatUnits(totalAmount, tokenDecimals), "token units");

console.log("Gross awarded:", formatUnits(releasedAmount, tokenDecimals), "token units");

console.log("Remaining:", formatUnits(remainingAmount, tokenDecimals), "token units");

console.log("Next milestone:", nextMilestoneIndex);

console.log("Work started:", workStarted);

console.log("Escrow state:", escrowState);

console.log("Milestone count:", milestoneCount);

for (let i = 0n; i < milestoneCount; i++) {
  const [amount, released] = await publicClient.readContract({
    address: ESCROW_ADDRESS,
    abi: escrowAbi,
    functionName: "getMilestone",
    args: [i],
  });

  console.log(
    `Milestone ${i}:`,
    formatUnits(amount, tokenDecimals),
    "token units",
    "| released:",
    released,
  );
}
