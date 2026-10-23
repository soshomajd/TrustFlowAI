type EscrowRecord = { status: string };

export async function startWorkWithRecovery(actions: {
  readStarted: () => Promise<boolean>;
  start: () => Promise<void>;
  sync?: () => Promise<unknown>;
}) {
  const alreadyStarted = await actions.readStarted();
  if (!alreadyStarted) await actions.start();
  if (!alreadyStarted && !await actions.readStarted()) {
    throw new Error("Work start is not yet confirmed on-chain. Wait for your wallet, then refresh the on-chain status before trying again.");
  }
  if (actions.sync) {
    try { await actions.sync(); }
    catch { throw new Error("Work started on-chain, but the site could not save its receipt. Use Refresh on-chain status; no new signature is needed."); }
  }
  return { alreadyStarted };
}

export async function releaseMilestoneWithRecovery(actions: {
  readReleased: () => Promise<boolean>;
  release: () => Promise<void>;
}) {
  if (await actions.readReleased()) return { alreadyReleased: true };
  await actions.release();
  if (!await actions.readReleased()) {
    throw new Error("Payment is not yet confirmed on-chain. Wait for your wallet, then retry approval to check the same milestone.");
  }
  return { alreadyReleased: false };
}

type ChainSync<T extends EscrowRecord> = {
  readState: () => Promise<number>;
  sync: () => Promise<T>;
};

async function syncConfirmed<T extends EscrowRecord>(sync: () => Promise<T>) {
  try {
    return await sync();
  } catch {
    throw new Error("The transaction is confirmed on-chain, but the site could not refresh. Use Refresh on-chain status. You do not need to sign again.");
  }
}

export async function fundEscrowWithRecovery<T extends EscrowRecord>(
  actions: ChainSync<T> & {
    readFunding: () => Promise<{ amount: bigint; allowance: bigint }>;
    approve: (amount: bigint) => Promise<void>;
    fund: () => Promise<void>;
  },
): Promise<T> {
  // A stale API record must never trigger another approval or fund call.
  const state = await actions.readState();
  if (state < 0 || state > 4) throw new Error("Unknown on-chain escrow status.");
  if (state !== 0) {
    const updated = await syncConfirmed(actions.sync);
    if (updated.status === "AwaitingFunding") {
      throw new Error("The on-chain status is ahead of the site. Use Refresh on-chain status before trying again.");
    }
    return updated;
  }

  const { amount, allowance } = await actions.readFunding();
  if (amount <= 0n) throw new Error("The escrow amount must be greater than zero.");
  if (allowance < amount) await actions.approve(amount);

  // The escrow may have been funded or cancelled in another tab meanwhile.
  const currentState = await actions.readState();
  if (currentState === 0) await actions.fund();
  else if (currentState < 0 || currentState > 4) throw new Error("Unknown on-chain escrow status.");

  const updated = await syncConfirmed(actions.sync);
  if (updated.status === "AwaitingFunding") {
    throw new Error("Funding is not yet reflected on the site. Use Refresh on-chain status before trying again.");
  }
  return updated;
}

export async function cancelEscrowWithRecovery<T extends EscrowRecord>(
  actions: ChainSync<T> & { cancel: () => Promise<void> },
): Promise<T> {
  const state = await actions.readState();
  if (state !== 3) {
    if (state !== 0 && state !== 1) {
      throw new Error("This escrow can no longer be cancelled. Refresh its on-chain status.");
    }
    await actions.cancel();
  }
  const updated = await syncConfirmed(actions.sync);
  if (updated.status !== "Cancelled") {
    throw new Error("Cancellation is not yet reflected on the site. Use Refresh on-chain status before trying again.");
  }
  return updated;
}
