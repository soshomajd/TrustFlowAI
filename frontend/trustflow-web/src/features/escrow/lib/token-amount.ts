import { parseUnits } from "viem";

// Backend amounts are decimal(18,2) values serialized as JSON numbers, so at
// most 2 decimal places are ever meaningful; toFixed rounds to the nearest
// representable decimal string before parseUnits converts it to an exact
// bigint. The contract itself requires an exact total match to accept
// fund(), so any mismatch fails loudly on-chain rather than silently.
export function toBaseUnits(amount: number, decimals: number): bigint {
  return parseUnits(amount.toFixed(decimals), decimals);
}
