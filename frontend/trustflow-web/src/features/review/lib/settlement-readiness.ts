import type { SettlementState } from "@/features/review/lib/settlement-recovery";

export type SettlementReadinessStatus =
  | "ready"
  | "waiting"
  | "changed"
  | "settled"
  | "released"
  | "closed"
  | "unavailable";

// This is a display/readiness check only. The settlement mutation independently
// reads the contract again before asking the human arbiter to sign.
export function settlementReadiness(
  expected: { status: string; kind: string; openedAt: string; index: number },
  state: SettlementState | null,
): SettlementReadinessStatus {
  if (expected.status !== "Open") return "closed";
  const openedAtMs = Date.parse(expected.openedAt);
  if (
    !state || !Number.isFinite(openedAtMs) || openedAtMs <= 0 ||
    !Number.isSafeInteger(expected.index) || expected.index < 0 ||
    (expected.kind !== "PaymentRequest" && expected.kind !== "RefundRequest") ||
    state.timestamp <= 0n
  ) return "unavailable";
  if (state.released) return state.settled ? "settled" : "released";
  if (state.settled) return "unavailable";
  if (
    state.kind !== (expected.kind === "PaymentRequest" ? 1 : 2) ||
    state.openedAt !== BigInt(Math.floor(openedAtMs / 1000)) ||
    state.nextIndex !== BigInt(expected.index)
  ) return "changed";
  return state.timestamp >= state.openedAt + 14n * 86400n ? "ready" : "waiting";
}

export function settlementReadinessMessage(status: SettlementReadinessStatus): string {
  switch (status) {
    case "ready": return "The chain confirms that the 14-day response window has ended for this open case. The contract is checked again before signing.";
    case "waiting": return "The 14-day response window has not ended on the chain. Signing remains disabled.";
    case "changed": return "The case on the contract has changed or was withdrawn. Read the contract again before deciding.";
    case "settled": return "This milestone is already settled on the chain. Read the contract again to recover the saved result.";
    case "released": return "The client already released this milestone outside arbitration. Read the contract again.";
    case "closed": return "This saved case is closed.";
    default: return "Current chain eligibility could not be confirmed. Signing remains disabled; retry the contract read.";
  }
}
