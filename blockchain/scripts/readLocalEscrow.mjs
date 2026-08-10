import { createPublicClient, formatUnits, http, parseAbi } from "viem";

const RPC_URL = "http://127.0.0.1:8545";

const ESCROW_ADDRESS = "0x9fE46736679d2D9a65F0992F2272dE9f3c7fa6e0";

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

console.log("Token:", token);
console.log("Client:", client);
console.log("Freelancer:", freelancer);

console.log("Total:", formatUnits(totalAmount, 6), "mUSDC");

console.log("Released:", formatUnits(releasedAmount, 6), "mUSDC");

console.log("Remaining:", formatUnits(remainingAmount, 6), "mUSDC");

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
    formatUnits(amount, 6),
    "mUSDC",
    "| released:",
    released,
  );
}
