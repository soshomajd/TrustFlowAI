import { decimalToBaseUnits } from "@/lib/money/decimal-amount";
export { decimalToBaseUnits as toBaseUnits };

// V3 totalAmount is cumulative DEPOSITS, zero before the first deposit. It
// cannot be used as the required amount of a new funding operation.
export function requiredFundingAmount(protocolVersion: number | undefined, stageAmount: string | undefined,
  contractTotal: bigint | undefined, decimals: number | undefined): bigint | null {
  if (decimals === undefined || decimals > 18) return null;
  if (protocolVersion === 3) return stageAmount === undefined ? null : decimalToBaseUnits(stageAmount, decimals);
  return contractTotal ?? null;
}
