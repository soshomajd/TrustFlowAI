import { getAccount, readContract, waitForTransactionReceipt, writeContract } from "wagmi/actions";
import type { Address } from "viem";
import { getEscrowStages, syncEscrow } from "@/features/escrow/api/escrow-api";
import { escrowAbi } from "@/features/escrow/lib/escrow-abi";
import { stagedEscrowAbi } from "@/features/escrow/lib/staged-escrow-abi";
import { erc20Abi } from "@/features/escrow/lib/erc20-abi";
import { toBaseUnits } from "@/features/escrow/lib/token-amount";
import { confirmEscrowTransaction } from "@/features/escrow/lib/transaction-confirmation";
import type { ProjectEscrow, EscrowFundingStage } from "@/features/escrow/types/escrow";
import { wagmiConfig } from "@/lib/wallet/wagmi-config";

export async function fundStagedMilestone(projectId: string, escrow: ProjectEscrow, stage: EscrowFundingStage) {
  if (!escrow.contractAddress || stage.chainIndex === null || !stage.reservationActive)
    throw new Error("Prepare this milestone before funding it.");
  const address = escrow.contractAddress as Address;
  const chainId = escrow.chainId;
  const token = escrow.tokenAddress as Address;
  const client = escrow.clientWalletAddress as Address;
  async function finish() {
    const updated = await syncEscrow(projectId, escrow.freelancerId);
    const recorded = (await getEscrowStages(projectId, escrow.freelancerId)).find(s => s.id === stage.id);
    if (!recorded?.fundedAt || recorded.fundingReference !== stage.fundingReference)
      throw new Error("Funding is confirmed on-chain but not saved yet. Refresh on-chain status; no new signature is needed.");
    if (updated.status === "Cancelled" || updated.status === "Terminated")
      throw new Error("This contract has ended. Refresh its status to review the payment and refund records.");
    return updated;
  }
  const account = getAccount(wagmiConfig);
  if (account.address?.toLowerCase() !== client?.toLowerCase()) throw new Error("Connect the client's verified wallet.");
  if (account.chainId !== chainId) throw new Error("Switch your wallet to the escrow's network before funding.");
  const alreadyFunded = await readContract(wagmiConfig, { address, abi: stagedEscrowAbi,
    functionName: "usedFundingReferences", args: [stage.fundingReference], chainId });
  if (alreadyFunded) return finish();
  const decimals = await readContract(wagmiConfig, { address: token, abi: erc20Abi, functionName: "decimals", chainId });
  const amount = toBaseUnits(stage.amountExact, decimals);
  if (stage.chainIndex === 0) {
    const [reference, terms, milestone] = await Promise.all([
      readContract(wagmiConfig, { address, abi: stagedEscrowAbi, functionName: "fundingReferences", args: [0n], chainId }),
      readContract(wagmiConfig, { address, abi: stagedEscrowAbi, functionName: "termsHashes", args: [0n], chainId }),
      readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "getMilestone", args: [0n], chainId }),
    ]);
    if (reference !== stage.fundingReference || terms !== stage.termsHash || milestone[0] !== amount)
      throw new Error("The contract milestone does not match the agreed terms and amount. Refresh before funding.");
  }
  const [balance, allowance, state, actualClient, actualToken] = await Promise.all([
    readContract(wagmiConfig, { address: token, abi: erc20Abi, functionName: "balanceOf", args: [client], chainId }),
    readContract(wagmiConfig, { address: token, abi: erc20Abi, functionName: "allowance", args: [client, address], chainId }),
    readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "escrowState", chainId }),
    readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "client", chainId }),
    readContract(wagmiConfig, { address, abi: escrowAbi, functionName: "token", chainId }),
  ]);
  if (actualClient.toLowerCase() !== client.toLowerCase() || actualToken.toLowerCase() !== token.toLowerCase())
    throw new Error("The contract does not match the agreed payment wallet and token.");
  if (amount <= 0n || balance < amount) throw new Error("Your token balance is too low to fund this milestone. Add tokens and resume funding.");
  if (state !== (stage.chainIndex === 0 ? 0 : 2)) throw new Error("Refresh on-chain status before funding this stage.");
  const key = `${chainId}:${address.toLowerCase()}:${stage.fundingReference}`;
  const wait = (hash: `0x${string}`) => waitForTransactionReceipt(wagmiConfig, { hash, chainId });
  if (allowance < amount)
    await confirmEscrowTransaction(`${key}:approve`, () => writeContract(wagmiConfig, {
      address: token, abi: erc20Abi, functionName: "approve", args: [address, amount], account: client, chainId,
    }), wait);
  // Check again after approval: another tab may have completed the same deposit.
  if (!await readContract(wagmiConfig, { address, abi: stagedEscrowAbi,
    functionName: "usedFundingReferences", args: [stage.fundingReference], chainId }))
    await confirmEscrowTransaction(`${key}:fund`, () => stage.chainIndex === 0
      ? writeContract(wagmiConfig, { address, abi: stagedEscrowAbi, functionName: "fund", account: client, chainId })
      : writeContract(wagmiConfig, { address, abi: stagedEscrowAbi, functionName: "appendAndFundMilestone",
        args: [amount, stage.fundingReference, stage.termsHash], account: client, chainId }), wait);
  if (!await readContract(wagmiConfig, { address, abi: stagedEscrowAbi,
    functionName: "usedFundingReferences", args: [stage.fundingReference], chainId }))
    throw new Error("The wallet transaction did not fund this milestone. Refresh on-chain status before trying again.");
  return finish();
}
