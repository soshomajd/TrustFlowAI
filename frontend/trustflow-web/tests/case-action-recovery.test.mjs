import assert from "node:assert/strict";
import { test } from "node:test";
import { createCaseActionRunner, caseWindowReadiness } from "../src/features/escrow/lib/recoverable-case-actions.ts";

const DAY = 86400n;
const openedAt = 1000n;
const expectedCase = { kind: "PaymentRequest", openedAt: new Date(Number(openedAt * 1000n)).toISOString() };
const base = { escrowState: 1, workStarted: true, kind: 0, openedAt: 0n, nextIndex: 0n,
  timestamp: openedAt + 90n * DAY, total: 400n, released: 0n, refunded: 0n,
  client: "client", freelancer: "freelancer", milestone: { index: 0n, amount: 100n, paid: 0n, released: false, settled: false } };
const open = { ...base, kind: 1, openedAt };
const actions = ["requestPayment", "requestRefund", "withdrawRequest", "settleStalledCase", "resign"];
function before(action) { return action === "withdrawRequest" || action === "settleStalledCase" ? open : base; }
function applied(action) {
  const initial = before(action);
  if (action === "requestPayment" || action === "requestRefund") return { ...initial, kind: action === "requestPayment" ? 1 : 2, openedAt: initial.timestamp + 1n, timestamp: initial.timestamp + 1n };
  if (action === "withdrawRequest") return { ...initial, kind: 0, openedAt: 0n };
  if (action === "resign") return { ...initial, escrowState: 4, refunded: 400n };
  return { ...initial, escrowState: 4, kind: 0, openedAt: 0n, nextIndex: 1n, released: 50n, refunded: 350n,
    milestone: { ...initial.milestone, released: true, settled: true, paid: 50n } };
}
function harness(action, changes = {}) {
  const run = createCaseActionRunner();
  const counters = { sends: 0, waits: 0, syncs: 0, reads: 0 };
  let current = before(action);
  const request = { key: "contract:action", action, index: 0,
    expectedCase: action === "withdrawRequest" || action === "settleStalledCase" ? expectedCase : undefined,
    read: async (index) => { counters.reads++; assert.ok(index === undefined || index === 0n); return current; },
    send: async () => { counters.sends++; return `0x${"1".repeat(64)}`; },
    wait: async () => { counters.waits++; current = applied(action); return { status: "success" }; },
    sync: async () => { counters.syncs++; },
    ...changes,
  };
  return { run: () => run(request), request, counters, setState: (next) => { current = next; } };
}
for (const action of actions) {
  test(`${action}: success requires a successful receipt and matching current state`, async () => {
    const f = harness(action); await f.run();
    assert.deepEqual(f.counters, { sends: 1, waits: 1, syncs: 1, reads: 2 });
  });
  test(`${action}: a mined revert cannot sync or report success`, async () => {
    const f = harness(action, { wait: async () => ({ status: "reverted" }) });
    await assert.rejects(f.run(), /reverted/);
    assert.equal(f.counters.sends, 1); assert.equal(f.counters.syncs, 0);
  });
  for (const mined of [false, true]) test(`${action}: lost ${mined ? "mined" : "pending"} receipt reuses the same submitted hash`, async () => {
    const f = harness(action); let waits = 0;
    f.request.wait = async () => {
      if (++waits === 1) { if (mined) f.setState(applied(action)); throw new Error("RPC response lost"); }
      f.setState(applied(action)); return { status: "success" };
    };
    await assert.rejects(f.run(), /confirmation could not be read/);
    await f.run(); assert.equal(f.counters.sends, 1); assert.equal(waits, 2); assert.equal(f.counters.syncs, 1);
  });
  test(`${action}: lost API response retries reconciliation without another signature`, async () => {
    const f = harness(action); let syncs = 0;
    f.request.sync = async () => { if (++syncs === 1) throw new Error("API offline"); };
    await assert.rejects(f.run(), /API offline/); await f.run();
    assert.equal(f.counters.sends, 1); assert.equal(f.counters.waits, 1); assert.equal(syncs, 2);
  });
  test(`${action}: an ineffective replacement or a different path cannot report success`, async () => {
    const f = harness(action, { wait: async () => ({ status: "success" }) });
    await assert.rejects(f.run(), /expected action is not confirmed/); assert.equal(f.counters.syncs, 0);
  });
}
test("same-page simultaneous retries share one signature and sync", async () => {
  const f = harness("resign");
  await Promise.all([f.run(), f.run(), f.run()]);
  assert.equal(f.counters.sends, 1); assert.equal(f.counters.syncs, 1);
});
test("wallet rejection remains retryable without retaining a nonexistent hash", async () => {
  const f = harness("requestPayment"); let sends = 0;
  f.request.send = async () => { if (++sends === 1) throw new Error("User rejected"); return `0x${"2".repeat(64)}`; };
  await assert.rejects(f.run(), /User rejected/); await f.run(); assert.equal(sends, 2); assert.equal(f.counters.syncs, 1);
});
test("already-open matching requests reconcile without a fresh signature", async () => {
  for (const action of ["requestPayment", "requestRefund"]) {
    const f = harness(action); f.setState({ ...open, kind: action === "requestPayment" ? 1 : 2 });
    await f.run(); assert.equal(f.counters.sends, 0); assert.equal(f.counters.syncs, 1);
  }
});
test("withdrawn/replaced cases, wrong order, unstarted/ended escrows and unreadable state never reach the wallet", async () => {
  for (const action of actions) for (const state of [{ ...before(action), nextIndex: 1n }, { ...before(action), workStarted: false }, { ...before(action), escrowState: 4 }]) {
    const f = harness(action); f.setState(state); await assert.rejects(f.run()); assert.equal(f.counters.sends, 0);
  }
  for (const action of ["withdrawRequest", "settleStalledCase"]) for (const changed of [{ ...open, kind: 2 }, { ...open, openedAt: openedAt + 1n }, base]) {
    const f = harness(action); f.setState(changed); await assert.rejects(f.run(), /changed or was withdrawn/); assert.equal(f.counters.sends, 0);
  }
  const unreadable = harness("resign", { read: async () => { throw new Error("RPC offline"); } });
  await assert.rejects(unreadable.run(), /RPC offline/); assert.equal(unreadable.counters.sends, 0);
});
test("90-day settlement uses the exact chain boundary and conserves the later-stage refund", async () => {
  const early = harness("settleStalledCase"); early.setState({ ...open, timestamp: openedAt + 90n * DAY - 1n });
  await assert.rejects(early.run(), /90-day silence window/); assert.equal(early.counters.sends, 0);
  const f = harness("settleStalledCase"); await f.run();
  const outcome = applied("settleStalledCase"); assert.equal(outcome.released + outcome.refunded, outcome.total);
  const wrong = harness("settleStalledCase", { wait: async () => ({ status: "success" }) });
  wrong.request.wait = async () => { wrong.setState({ ...outcome, milestone: { ...outcome.milestone, paid: 40n } }); return { status: "success" }; };
  await assert.rejects(wrong.run(), /expected action is not confirmed/);
});
test("participant response/fallback eligibility ignores browser clock and refuses unknown or replaced cases", () => {
  const clock = Date.now;
  try {
    Date.now = () => Date.parse("2099-01-01T00:00:00Z");
    assert.deepEqual(caseWindowReadiness({ ...open, timestamp: openedAt + 14n * DAY - 1n }, expectedCase, 0), { known: true, reviewerReady: false, canSplit: false });
    assert.deepEqual(caseWindowReadiness({ ...open, timestamp: openedAt + 14n * DAY }, expectedCase, 0), { known: true, reviewerReady: true, canSplit: false });
    assert.equal(caseWindowReadiness({ ...open, timestamp: openedAt + 90n * DAY - 1n }, expectedCase, 0).canSplit, false);
    Date.now = () => 1000;
    assert.equal(caseWindowReadiness(open, expectedCase, 0).canSplit, true);
    for (const state of [null, { ...open, kind: 0 }, { ...open, openedAt: openedAt + 1n }, { ...open, nextIndex: 1n }, { ...open, escrowState: 4 }, { ...open, milestone: null }]) {
      const result = caseWindowReadiness(state, expectedCase, 0); assert.equal(result.known, false); assert.equal(result.canSplit, false);
    }
  } finally { Date.now = clock; }
});
