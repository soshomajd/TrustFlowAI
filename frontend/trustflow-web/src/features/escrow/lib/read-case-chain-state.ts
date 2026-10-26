import { isAddress, parseAbi } from "viem";
import { getPublicClient } from "wagmi/actions";

import type { CaseChainState } from "@/features/escrow/lib/recoverable-case-actions";
import { configuredChain, wagmiConfig } from "@/lib/wallet/wagmi-config";

const caseStateAbi = parseAbi([
  "function client() view returns (address)",
  "function freelancer() view returns (address)",
  "function workStarted() view returns (bool)",
  "function escrowState() view returns (uint8)",
  "function nextMilestoneIndex() view returns (uint256)",
  "function getOpenCase() view returns (uint8 kind, uint256 openedAt)",
  "function totalAmount() view returns (uint256)",
  "function releasedAmount() view returns (uint256)",
  "function refundedAmount() view returns (uint256)",
  "function getMilestone(uint256 index) view returns (uint256 amount, bool released)",
  "function getMilestonePaid(uint256 index) view returns (uint256)",
  "function isSettledByCase(uint256 index) view returns (bool)",
]);

export async function readCaseChainState(address: string, chainId: number, index?: bigint): Promise<CaseChainState> {
  if (!isAddress(address) || chainId !== configuredChain.id) throw new Error("This escrow network or contract is not supported.");
  const client = getPublicClient(wagmiConfig, { chainId });
  if (!client || await client.getChainId() !== chainId) throw new Error("The RPC network does not match this escrow.");
  const block = await client.getBlock({ blockTag: "latest" });
  if (block.number === null || !block.hash || block.timestamp <= 0n) throw new Error("A confirmed chain block could not be read.");
  const contract = { address, abi: caseStateAbi, blockNumber: block.number } as const;
  const [escrowState, workStarted, open, nextIndex, total, released, refunded, owner, freelancer] = await Promise.all([
    client.readContract({ ...contract, functionName: "escrowState" }),
    client.readContract({ ...contract, functionName: "workStarted" }),
    client.readContract({ ...contract, functionName: "getOpenCase" }),
    client.readContract({ ...contract, functionName: "nextMilestoneIndex" }),
    client.readContract({ ...contract, functionName: "totalAmount" }),
    client.readContract({ ...contract, functionName: "releasedAmount" }),
    client.readContract({ ...contract, functionName: "refundedAmount" }),
    client.readContract({ ...contract, functionName: "client" }),
    client.readContract({ ...contract, functionName: "freelancer" }),
  ]);
  let milestone: CaseChainState["milestone"] = null;
  // A terminal escrow's next index can be outside the array. Only read an
  // explicit prior index when checking the result of our submitted action.
  if (index !== undefined || escrowState === 1) {
    const stageIndex = index ?? nextIndex;
    const [stage, paid, settled] = await Promise.all([
      client.readContract({ ...contract, functionName: "getMilestone", args: [stageIndex] }),
      client.readContract({ ...contract, functionName: "getMilestonePaid", args: [stageIndex] }),
      client.readContract({ ...contract, functionName: "isSettledByCase", args: [stageIndex] }),
    ]);
    milestone = { index: stageIndex, amount: stage[0], released: stage[1], paid, settled };
  }
  const canonical = await client.getBlock({ blockNumber: block.number });
  if (canonical.hash !== block.hash) throw new Error("The chain changed during the read. Refresh on-chain status.");
  return { escrowState, workStarted, kind: open[0], openedAt: open[1], nextIndex,
    timestamp: block.timestamp, total, released, refunded, client: owner, freelancer, milestone };
}
