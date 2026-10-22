import { formatMoney } from "@/lib/money/exact-money";
import { formatUnits } from "viem";
import { decimalToBaseUnits } from "@/lib/money/decimal-amount";

// Display-only preview. The contract computes the authoritative fee once for
// each gross award, in integer base units; never round it to currency cents.
export function previewPlatformFee(grossText: string, decimals: number, basisPoints: number) {
  const gross = decimalToBaseUnits(grossText, decimals);
  if (basisPoints !== 0 && basisPoints !== 1000) throw new Error("Unsupported fee policy");
  const fee = basisPoints === 1000 ? gross / 10n : 0n;
  return { fee: formatUnits(fee, decimals), net: formatUnits(gross - fee, decimals) };
}

export function formatTokenPayment(value: number | string) {
  return formatMoney(value);
}
