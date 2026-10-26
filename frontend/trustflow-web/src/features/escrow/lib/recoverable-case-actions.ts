import type { Hash } from "viem";

export type CaseAction = "requestPayment" | "requestRefund" | "withdrawRequest" | "settleStalledCase" | "resign";
export type ExpectedCase = { kind: "PaymentRequest" | "RefundRequest"; openedAt: string };
export type CaseChainState = {
  escrowState: number;
  workStarted: boolean;
  kind: number;
  openedAt: bigint;
  nextIndex: bigint;
  timestamp: bigint;
  total: bigint;
  released: bigint;
  refunded: bigint;
  client: string;
  freelancer: string;
  milestone: { index: bigint; amount: bigint; paid: bigint; released: boolean; settled: boolean } | null;
};
type ActionRequest = {
  key: string;
  action: CaseAction;
  index?: number;
  expectedCase?: ExpectedCase;
  read: (index?: bigint) => Promise<CaseChainState>;
  send: (before: CaseChainState) => Promise<Hash>;
  wait: (hash: Hash) => Promise<{ status: "success" | "reverted" }>;
  sync: () => Promise<unknown>;
};
type Attempt = { before: CaseChainState; hash?: Hash; confirmed: boolean };

function requireCurrentMilestone(request: ActionRequest, state: CaseChainState) {
  if (request.index !== undefined && (!Number.isSafeInteger(request.index) || request.index < 0 || BigInt(request.index) !== state.nextIndex)) {
    throw new Error("The next unpaid milestone changed. Refresh on-chain status before trying again.");
  }
  if (state.timestamp <= 0n || state.escrowState !== 1 || !state.workStarted || !state.milestone ||
      state.milestone.index !== state.nextIndex || state.milestone.released || state.milestone.settled) {
    throw new Error("This escrow no longer has that active unpaid milestone. Refresh on-chain status.");
  }
}

export function matchesExpectedCase(state: CaseChainState, expectedCase?: ExpectedCase): boolean {
  const openedAt = expectedCase ? Date.parse(expectedCase.openedAt) : NaN;
  return !!expectedCase && Number.isFinite(openedAt) && openedAt > 0 &&
    state.kind === (expectedCase.kind === "PaymentRequest" ? 1 : 2) &&
    state.openedAt === BigInt(Math.floor(openedAt / 1000));
}

export function caseWindowReadiness(state: CaseChainState | null, expectedCase: ExpectedCase, index: number) {
  if (!state || !Number.isSafeInteger(index) || index < 0 || state.timestamp <= 0n ||
      state.escrowState !== 1 || !state.workStarted || state.nextIndex !== BigInt(index) ||
      !state.milestone || state.milestone.index !== BigInt(index) || state.milestone.released || state.milestone.settled ||
      state.timestamp < state.openedAt || !matchesExpectedCase(state, expectedCase)) {
    return { known: false, reviewerReady: false, canSplit: false };
  }
  return { known: true, reviewerReady: state.timestamp >= state.openedAt + 14n * 86400n,
    canSplit: state.timestamp >= state.openedAt + 90n * 86400n };
}

function prepare(request: ActionRequest, state: CaseChainState): boolean {
  requireCurrentMilestone(request, state);
  if (request.action === "requestPayment" || request.action === "requestRefund") {
    const expectedKind = request.action === "requestPayment" ? 1 : 2;
    // A lost API response after opening is recovered by recording the current
    // request, without asking for another signature or opening a second case.
    if (state.kind === expectedKind && state.openedAt > 0n && state.openedAt <= state.timestamp) return true;
    if (state.kind !== 0) throw new Error("A different request is already open on this escrow. Refresh on-chain status.");
  } else if (request.action !== "resign") {
    if (!matchesExpectedCase(state, request.expectedCase)) throw new Error("The request changed or was withdrawn. Refresh on-chain status before trying again.");
    if (request.action === "settleStalledCase" && state.timestamp < state.openedAt + 90n * 86400n) {
      throw new Error("The 90-day silence window has not ended on the chain.");
    }
  }
  return false;
}

function applied(action: CaseAction, before: CaseChainState, after: CaseChainState): boolean {
  if (after.total !== before.total || !after.workStarted) return false;
  const sameMoney = after.released === before.released && after.refunded === before.refunded;
  const sameStage = after.nextIndex === before.nextIndex && after.milestone?.index === before.nextIndex &&
    after.milestone.released === false && after.milestone.paid === before.milestone?.paid;
  switch (action) {
    case "requestPayment":
    case "requestRefund":
      return after.escrowState === 1 && sameMoney && sameStage &&
        after.kind === (action === "requestPayment" ? 1 : 2) &&
        after.openedAt >= before.timestamp && after.openedAt <= after.timestamp;
    case "withdrawRequest":
      return after.escrowState === 1 && sameMoney && sameStage && after.kind === 0;
    case "resign":
      return after.escrowState === 4 && sameStage && after.kind === 0 &&
        after.released === before.released && after.refunded === before.total - before.released;
    case "settleStalledCase": {
      const gross = before.milestone!.amount / 2n;
      return (after.escrowState === 2 || after.escrowState === 4) && after.kind === 0 &&
        after.nextIndex === before.nextIndex + 1n && after.milestone?.index === before.nextIndex &&
        after.milestone.released && after.milestone.settled && after.milestone.paid === gross &&
        after.released === before.released + gross && after.refunded === before.total - after.released;
    }
  }
}

// Page-memory only: retain the exact submitted hash and original case across a
// failed receipt/API response. A full reload still requires refreshing first.
export function createCaseActionRunner() {
  const attempts = new Map<string, Attempt>();
  const running = new Map<string, Promise<void>>();
  return function run(request: ActionRequest): Promise<void> {
    const existing = running.get(request.key);
    if (existing) return existing;
    const operation = (async () => {
      let attempt = attempts.get(request.key);
      if (!attempt) {
        if (request.index !== undefined && (!Number.isSafeInteger(request.index) || request.index < 0)) {
          throw new Error("The milestone position is invalid. Refresh on-chain status.");
        }
        const before = await request.read(request.index === undefined ? undefined : BigInt(request.index));
        if (prepare(request, before)) { await request.sync(); return; }
        attempt = { before, confirmed: false };
        attempts.set(request.key, attempt);
      }
      if (!attempt.hash) {
        try { attempt.hash = await request.send(attempt.before); }
        catch (error) { attempts.delete(request.key); throw error; }
      }
      if (!attempt.confirmed) {
        let receipt;
        try { receipt = await request.wait(attempt.hash); }
        catch { throw new Error("A transaction was submitted, but its confirmation could not be read. Wait for your wallet, then retry to check the same transaction or refresh on-chain status."); }
        if (receipt.status !== "success") {
          attempts.delete(request.key);
          throw new Error("The transaction reverted. Refresh on-chain status before trying again.");
        }
        attempt.confirmed = true;
      }
      const after = await request.read(attempt.before.nextIndex);
      if (!applied(request.action, attempt.before, after)) {
        throw new Error("The expected action is not confirmed in the current contract state, or another action changed it. Refresh on-chain status before continuing.");
      }
      await request.sync();
      attempts.delete(request.key);
    })();
    running.set(request.key, operation);
    void operation.finally(() => running.delete(request.key)).catch(() => {});
    return operation;
  };
}

export const runCaseActionWithRecovery = createCaseActionRunner();
