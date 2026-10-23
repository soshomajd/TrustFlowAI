export type SettlementState = {
  released: boolean;
  settled: boolean;
  kind: number;
  openedAt: bigint;
  nextIndex: bigint;
  timestamp: bigint;
};

export async function settleWithRecovery(actions: {
  expectedKind: number;
  expectedOpenedAt: bigint;
  index: bigint;
  read: () => Promise<SettlementState>;
  settle: () => Promise<void>;
}) {
  const state = await actions.read();
  if (state.released && state.settled) return { alreadySettled: true };
  if (state.released) throw new Error("This milestone was already released outside arbitration. Refresh on-chain status.");
  if (state.kind !== actions.expectedKind || state.openedAt !== actions.expectedOpenedAt || state.nextIndex !== actions.index) {
    throw new Error("The on-chain case changed or was withdrawn. Refresh this case before signing.");
  }
  if (state.timestamp < state.openedAt + 14n * 86400n) {
    throw new Error("The 14-day response window has not ended on the chain.");
  }
  await actions.settle();
  const confirmed = await actions.read();
  if (!confirmed.released || !confirmed.settled) {
    throw new Error("The settlement is not yet confirmed on-chain. Refresh on-chain status before retrying.");
  }
  return { alreadySettled: false };
}
