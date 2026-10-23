import { decimalToBaseUnits, formatBaseUnits } from "@/lib/money/decimal-amount";

// Percentages have two decimal places; awards floor only at the token's
// smallest unit. The unallocated remainder belongs to the client.
export function percentageSettlement(amount: string, percent: string, decimals: number) {
  const text = percent.trim();
  if (!/^\d{1,3}(?:\.\d{1,2})?$/.test(text)) {
    throw new Error("Enter a percentage from 0 to 100 with at most 2 decimals.");
  }
  const basisPoints = decimalToBaseUnits(text, 2);
  if (basisPoints > 10000n) throw new Error("The percentage cannot exceed 100.");
  const total = decimalToBaseUnits(amount, decimals);
  const gross = total * basisPoints / 10000n;
  const plain = (value: bigint) => formatBaseUnits(value, decimals).replaceAll(",", "");
  return { freelancer: plain(gross), client: plain(total - gross), rounded: total * basisPoints % 10000n !== 0n };
}

export function getSettlementBaseUnits(
  freelancerText: string,
  displayedMilestoneAmount: number | string,
  decimals: number,
  onChainAmount: bigint,
) {
  const amount = decimalToBaseUnits(freelancerText, decimals);
  if (decimalToBaseUnits(displayedMilestoneAmount, decimals) !== onChainAmount) {
    throw new Error("The displayed case amount does not match the exact on-chain amount. This settlement cannot be signed safely from this page.");
  }
  if (amount > onChainAmount) {
    throw new Error("The freelancer amount cannot exceed the on-chain milestone amount.");
  }
  return amount;
}
