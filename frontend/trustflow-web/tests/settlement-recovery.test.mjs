import assert from "node:assert/strict";
import { test } from "node:test";
import { registerHooks } from "node:module";
import { settleWithRecovery } from "../src/features/review/lib/settlement-recovery.ts";
import { createTransactionConfirmer } from "../src/features/escrow/lib/transaction-confirmation.ts";
registerHooks({ resolve(specifier, context, nextResolve) {
  return nextResolve(specifier.startsWith("@/") ? new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url).href : specifier, context);
} });
const { percentageSettlement, getSettlementBaseUnits } = await import("../src/features/review/lib/settlement-amount.ts");
const { decimalToBaseUnits } = await import("../src/lib/money/decimal-amount.ts");
const { previewPlatformFee } = await import("../src/features/escrow/lib/platform-fee.ts");
for (const [total, percent, gross, refund, rounded] of [
  ["100", "20", "20", "80", false], ["100", "0", "0", "100", false],
  ["100", "100", "100", "0", false], ["0.01", "20", "0.002", "0.008", false],
  ["0.000001", "20", "0", "0.000001", true], ["0.000011", "50", "0.000005", "0.000006", true],
  ["9007199254740991.01", "33.33", "3002099511605172.303633", "6005099743135818.706367", false],
]) test(`percentage ${percent} of ${total} is exact at token precision`, () => {
  const result = percentageSettlement(total, percent, 6);
  assert.deepEqual(result, { freelancer: gross, client: refund, rounded });
  const preview = previewPlatformFee(gross, 6, 1000);
  assert.equal(decimalToBaseUnits(preview.fee, 6) + decimalToBaseUnits(preview.net, 6) + decimalToBaseUnits(refund, 6), decimalToBaseUnits(total, 6));
  assert.equal(getSettlementBaseUnits(gross, total, 6, decimalToBaseUnits(total, 6)), decimalToBaseUnits(gross, 6));
});
test("invalid percentages or unsupported precision never become a signable amount", () => {
  for (const percent of ["", "-1", "100.01", "101", "1e2", "NaN", "20.001", "20%", "1,5"]) assert.throws(() => percentageSettlement("100", percent, 6));
  assert.throws(() => percentageSettlement("0.0000001", "20", 6));
});
test("percentage rounding conserves base units for every supported token precision", () => {
  for (let decimals = 0; decimals <= 18; decimals++) for (const percent of ["0", "0.01", "20", "33.33", "99.99", "100"]) {
    const total = `1234567890123456789${decimals ? "." + "1".repeat(decimals) : ""}`;
    const split = percentageSettlement(total, percent, decimals);
    const units = decimalToBaseUnits(total, decimals), gross = decimalToBaseUnits(split.freelancer, decimals);
    const basis = decimalToBaseUnits(percent, 2);
    assert.equal(gross + decimalToBaseUnits(split.client, decimals), units);
    assert.ok(gross * 10000n <= units * basis);
    assert.ok((gross + 1n) * 10000n > units * basis);
  }
});
const openedAt = 1000n, index = 0n;
const state = { released: false, settled: false, kind: 1, openedAt, nextIndex: index, timestamp: openedAt + 14n * 86400n };
const args = { expectedKind: 1, expectedOpenedAt: openedAt, index };
test("already settled recovers without another signature or payment", async () => {
  assert.deepEqual(await settleWithRecovery({ ...args, read: async () => ({ ...state, released: true, settled: true }), settle: async () => assert.fail("no second signature") }), { alreadySettled: true });
});
test("withdrawn, replaced, early, out-of-order and directly released cases cannot reach the wallet", async () => {
  for (const change of [{ kind: 0 }, { openedAt: 1001n }, { kind: 2 }, { nextIndex: 1n }, { timestamp: state.timestamp - 1n }, { released: true }])
    await assert.rejects(settleWithRecovery({ ...args, read: async () => ({ ...state, ...change }), settle: async () => assert.fail("must refuse before signing") }));
});
test("unreadable chain state cannot reach the wallet", async () => {
  await assert.rejects(settleWithRecovery({ ...args, read: async () => { throw new Error("RPC offline"); }, settle: async () => assert.fail("no signature") }), /RPC offline/);
});
test("wallet rejection remains retryable and confirmed settlement is checked", async () => {
  let rejected = true, applied = false;
  const actions = { ...args, read: async () => ({ ...state, released: applied, settled: applied }), settle: async () => { if (rejected) throw new Error("Rejected"); applied = true; } };
  await assert.rejects(settleWithRecovery(actions), /Rejected/); rejected = false;
  assert.equal((await settleWithRecovery(actions)).alreadySettled, false);
});
test("reverted or successful replacement without settlement cannot report success", async () => {
  const confirm = createTransactionConfirmer();
  await assert.rejects(settleWithRecovery({ ...args, read: async () => state,
    settle: () => confirm("case", async () => `0x${"1".repeat(64)}`, async () => ({ status: "reverted" })) }), /reverted/);
  await assert.rejects(settleWithRecovery({ ...args, read: async () => state, settle: async () => {} }), /not yet confirmed/);
});
for (const mined of [false, true]) test(`receipt response lost (${mined ? "mined" : "pending"}): retry never sends twice`, async () => {
  const confirm = createTransactionConfirmer(); let applied = false, sends = 0, waits = 0;
  const actions = { ...args, read: async () => ({ ...state, released: applied, settled: applied }),
    settle: () => confirm("same-case", async () => { sends++; return `0x${"2".repeat(64)}`; }, async () => {
      if (++waits === 1) { applied = mined; throw new Error("lost receipt"); }
      applied = true; return { status: "success" };
    }) };
  await assert.rejects(settleWithRecovery(actions), /confirmation could not be read/);
  await settleWithRecovery(actions); assert.equal(sends, 1);
});
test("concurrent confirmation for one case shares one wallet request", async () => {
  const confirm = createTransactionConfirmer(); let applied = false, sends = 0;
  const actions = { ...args, read: async () => ({ ...state, released: applied, settled: applied }),
    settle: () => confirm("same-case", async () => { sends++; return `0x${"3".repeat(64)}`; }, async () => { applied = true; return { status: "success" }; }) };
  await Promise.all([settleWithRecovery(actions), settleWithRecovery(actions)]); assert.equal(sends, 1);
});
