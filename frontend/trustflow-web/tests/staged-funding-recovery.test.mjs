import assert from "node:assert/strict";
import { test } from "node:test";
import { registerHooks } from "node:module";
import { existsSync } from "node:fs";
import { fileURLToPath } from "node:url";

const moduleUrl = source => `data:text/javascript,${encodeURIComponent(source)}`;
registerHooks({ resolve(specifier, context, next) {
  if (specifier === "wagmi/actions") return { shortCircuit: true, url: moduleUrl(`
    export const getAccount = () => globalThis.walletFixture.account;
    export const readContract = (_config,args) => globalThis.walletFixture.read(args);
    export const writeContract = (_config,args) => globalThis.walletFixture.write(args);
    export const waitForTransactionReceipt = (_config,args) => globalThis.walletFixture.wait(args.hash);
  `) };
  if (specifier === "@/lib/wallet/wagmi-config") return { shortCircuit: true, url: moduleUrl("export const wagmiConfig = {};") };
  if (specifier === "@/features/escrow/api/escrow-api") return { shortCircuit: true, url: moduleUrl(`
    export const syncEscrow = () => globalThis.walletFixture.sync();
    export const getEscrowStages = () => globalThis.walletFixture.savedStages();
  `) };
  if (specifier.startsWith("@/")) {
    const url = new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url);
    if (existsSync(fileURLToPath(url))) return next(url.href, context);
  }
  return next(specifier, context);
} });
const { fundStagedMilestone } = await import("../src/features/escrow/lib/fund-staged-milestone.ts");
const { createTransactionConfirmer } = await import("../src/features/escrow/lib/transaction-confirmation.ts");
const { requiredFundingAmount } = await import("../src/features/escrow/lib/token-amount.ts");
test("funding readiness uses the current stage price, never zero or the cumulative deposits", () => {
  assert.equal(requiredFundingAmount(3, "100", 0n, 6), 100_000_000n);
  assert.equal(requiredFundingAmount(3, "50", 200_000_000n, 6), 50_000_000n);
  assert.equal(requiredFundingAmount(3, undefined, 0n, 6), null);
  assert.equal(requiredFundingAmount(2, undefined, 300_000_000n, 6), 300_000_000n);
});
let fixtureId = 0;
function fixture(index = 0) {
  const number = ++fixtureId;
  const client = `0x${"4".repeat(40)}`;
  const token = `0x${"3".repeat(40)}`;
  const reference = `0x${number.toString(16).padStart(64, "0")}`;
  const terms = `0x${"a".repeat(64)}`;
  const stage = { id: `stage-${number}`, chainIndex: index, amountExact: "100", reservationActive: true,
    fundingReference: reference, termsHash: terms, fundedAt: null };
  const escrow = { contractAddress: `0x${"5".repeat(40)}`, chainId: 31337,
    tokenAddress: token, clientWalletAddress: client, freelancerId: "freelancer", protocolVersion: 3 };
  const f = { account: { address: client, chainId: 31337 }, balance: 100_000_000n, allowance: 0n,
    state: index === 0 ? 0 : 2, funded: false, apiSaved: false, apiDown: false, apiLag: false,
    rejectApproval: false, reverted: false, replacement: false, waitFails: 0, sends: [], receipts: [], pending: new Map(),
    async read(_config, args) {
      return ({ usedFundingReferences: f.funded, decimals: 6, balanceOf: f.balance, allowance: f.allowance,
        escrowState: f.state, client, token, fundingReferences: reference, termsHashes: terms, getMilestone: [100_000_000n, false] })[args.functionName];
    },
    async write(args) {
      if (args.functionName === "approve" && f.rejectApproval) throw new Error("User rejected approval");
      f.sends.push(args.functionName);
      const hash = `0x${(number * 100 + f.sends.length).toString(16).padStart(64, "0")}`;
      f.pending.set(hash, args);
      return hash;
    },
    async wait(hash) {
      f.receipts.push(hash);
      if (f.waitFails-- > 0) throw new Error("RPC timeout");
      if (f.reverted) return { status: "reverted" };
      const args = f.pending.get(hash);
      if (!f.replacement) {
        if (args.functionName === "approve") f.allowance = args.args[1];
        else {
          if (index > 0) assert.deepEqual(args.args, [100_000_000n, reference, terms]);
          assert.equal(f.funded, false); assert.ok(f.allowance >= 100_000_000n);
          f.allowance -= 100_000_000n; f.balance -= 100_000_000n; f.funded = true; f.state = 1;
        }
      }
      return { status: "success" };
    },
    async sync() {
      if (f.apiDown) throw new Error("API offline");
      if (!f.apiLag) f.apiSaved = f.funded;
      return { status: f.apiSaved ? "Funded" : "AwaitingFunding" };
    },
    async savedStages() { return [{ ...stage, fundedAt: f.apiSaved ? "2026-10-28" : null }]; },
  };
  // Match wagmi's (config, options) calling convention in the mock adapter.
  f.read = f.read.bind(f);
  globalThis.walletFixture = { ...f, get account() { return f.account; },
    read: args => f.read({}, args), write: args => f.write(args), wait: hash => f.wait(hash),
    sync: () => f.sync(), savedStages: () => f.savedStages() };
  return { f, run: () => fundStagedMilestone("project", escrow, stage) };
}

test("initial and appended stages fund the exact amount and save the deposit before reporting success", async () => {
  for (const index of [0, 1]) {
    const { f, run } = fixture(index);
    assert.equal((await run()).status, "Funded");
    assert.equal(f.balance, 0n);
    assert.deepEqual(f.sends, ["approve", index === 0 ? "fund" : "appendAndFundMilestone"]);
    await run(); assert.equal(f.sends.length, 2);
  }
});
test("a rejected approval leaves the stage unfunded and retryable", async () => {
  const { f, run } = fixture(); f.rejectApproval = true;
  await assert.rejects(run, /rejected approval/); assert.equal(f.funded, false); assert.equal(f.sends.length, 0);
  f.rejectApproval = false; await run(); assert.equal(f.funded, true);
});
test("insufficient tokens or the wrong network never asks for a signature", async () => {
  const { f, run } = fixture(); f.balance = 99_999_999n;
  await assert.rejects(run, /balance is too low/); assert.equal(f.sends.length, 0);
  f.account.chainId = 1; await assert.rejects(run, /Switch your wallet/); assert.equal(f.sends.length, 0);
});
test("a reverted funding receipt cannot report success and the approval is reused on retry", async () => {
  const { f, run } = fixture(); f.allowance = 100_000_000n; f.reverted = true;
  await assert.rejects(run, /reverted/); assert.equal(f.funded, false);
  f.reverted = false; await run(); assert.deepEqual(f.sends, ["fund", "fund"]);
});
test("a successful replacement receipt without the deposit cannot report funding success", async () => {
  const { f, run } = fixture(); f.allowance = 100_000_000n; f.replacement = true;
  await assert.rejects(run, /did not fund this milestone/); assert.equal(f.apiSaved, false);
});
test("a receipt outage waits for the same transaction and sends no duplicate deposit", async () => {
  const { f, run } = fixture(); f.allowance = 100_000_000n; f.waitFails = 1;
  await assert.rejects(run, /confirmation could not be read/);
  await run(); assert.deepEqual(f.sends, ["fund"]); assert.equal(f.receipts[0], f.receipts[1]);
});
test("a lost API response after the deposit reconciles without another signature", async () => {
  const { f, run } = fixture(); f.apiDown = true;
  await assert.rejects(run, /API offline/); assert.equal(f.funded, true);
  f.apiDown = false; await run(); assert.deepEqual(f.sends, ["approve", "fund"]);
});
test("an API reply without saved funding evidence is not a successful hire", async () => {
  const { f, run } = fixture(); f.apiLag = true;
  await assert.rejects(run, /not saved yet/); assert.equal(f.funded, true);
  f.apiLag = false; await run(); assert.equal(f.sends.length, 2);
});
test("a reload reuses the persisted pending hash without persisting account credentials", async () => {
  const store = new Map(); globalThis.window = { localStorage: {
    getItem: key => store.get(key), setItem: (key, value) => store.set(key, value), removeItem: key => store.delete(key),
  } };
  const hash = `0x${"b".repeat(64)}`;
  try {
    await assert.rejects(createTransactionConfirmer()("reload-case", async () => hash, async () => { throw new Error("offline"); }));
    assert.deepEqual([...store.values()], [hash]);
    await createTransactionConfirmer()("reload-case", async () => { assert.fail("must not send again"); }, async supplied => {
      assert.equal(supplied, hash); return { status: "success" };
    });
    assert.equal(store.size, 0);
  } finally { delete globalThis.window; }
});
