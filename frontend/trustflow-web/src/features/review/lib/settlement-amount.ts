import { decimalToBaseUnits } from "@/lib/money/decimal-amount";

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
