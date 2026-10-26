import assert from "node:assert/strict";
import { test } from "node:test";
import { registerHooks } from "node:module";
import { settlementReadiness } from "../src/features/review/lib/settlement-readiness.ts";

const opened = "2026-10-01T12:00:00Z";
const openedAt = BigInt(Date.parse(opened) / 1000);
const expected = { status: "Open", kind: "PaymentRequest", openedAt: opened, index: 2 };
const state = { released: false, settled: false, kind: 1, openedAt, nextIndex: 2n, timestamp: openedAt + 14n * 86400n };

test("readiness follows the exact chain-time boundary, not the host clock", () => {
  const original = Date.now;
  try {
    Date.now = () => Number(openedAt * 1000n);
    assert.equal(settlementReadiness(expected, state), "ready");
    Date.now = () => Number((state.timestamp + 90n * 86400n) * 1000n);
    assert.equal(settlementReadiness(expected, { ...state, timestamp: state.timestamp - 1n }), "waiting");
    assert.equal(settlementReadiness(expected, { ...state, timestamp: state.timestamp + 1n }), "ready");
  } finally { Date.now = original; }
});
test("a replaced, withdrawn, out-of-order or mismatched case never becomes ready", () => {
  for (const change of [{ kind: 0 }, { kind: 2 }, { openedAt: openedAt + 1n }, { nextIndex: 3n }])
    assert.equal(settlementReadiness(expected, { ...state, ...change }), "changed");
  assert.equal(settlementReadiness({ ...expected, kind: "RefundRequest" }, { ...state, kind: 2 }), "ready");
});
test("closed, already settled, direct release and unreadable states cannot enable a signature", () => {
  assert.equal(settlementReadiness({ ...expected, status: "Settled" }, state), "closed");
  assert.equal(settlementReadiness(expected, { ...state, released: true, settled: true }), "settled");
  assert.equal(settlementReadiness(expected, { ...state, released: true }), "released");
  for (const unavailable of [null, { ...state, timestamp: 0n }, { ...state, settled: true }])
    assert.equal(settlementReadiness(expected, unavailable), "unavailable");
  for (const invalid of [{ index: -1 }, { index: 0.5 }, { openedAt: "invalid" }, { kind: "Unknown" }])
    assert.equal(settlementReadiness({ ...expected, ...invalid }, state), "unavailable");
});

const code = (source) => ({ url: `data:text/javascript,${encodeURIComponent(source)}`, shortCircuit: true });
registerHooks({ resolve(specifier, context, nextResolve) {
  if (specifier === "@tanstack/react-query") return code('export function useQuery(options){globalThis.readinessOptions=options;return globalThis.readinessQueryState ?? {isPending:true};}');
  if (specifier === "wagmi/actions") return code('export function getPublicClient(){return globalThis.readinessClient;}');
  if (specifier === "@/lib/wallet/wagmi-config") return code('export const configuredChain={id:31337};export const wagmiConfig={};');
  return nextResolve(specifier.startsWith("@/") ? new URL(`../src/${specifier.slice(2)}.ts`, import.meta.url).href : specifier, context);
} });
// useQuery is stubbed above so these calls inspect query configuration;
// they do not mount a React component or execute React's hook dispatcher.
const { useReviewCaseReadiness: inspectReadinessQuery } = await import("../src/features/review/hooks/use-review-case-readiness.ts");
const detail = { case: { ...expected, id: "case-1", isReady: false }, chainId: 31337,
  contractAddress: "0x0000000000000000000000000000000000000001", onChainMilestoneIndex: expected.index };
function client(changes = {}) {
  const reads = [];
  return {
    reads,
    getChainId: async () => 31337,
    getBlock: async () => ({ number: 7n, hash: "canonical-block", timestamp: state.timestamp }),
    readContract: async (request) => {
      reads.push(request);
      return { getMilestone: [100000000n, false], isSettledByCase: false,
        getOpenCase: [1, openedAt], nextMilestoneIndex: 2n }[request.functionName];
    },
    ...changes,
  };
}
function options(source = detail, rpc = client()) {
  globalThis.readinessClient = rpc; globalThis.readinessQueryState = undefined;
  inspectReadinessQuery(source);
  return globalThis.readinessOptions;
}
test("readiness query verifies RPC network and pins every case read to its clock block", async () => {
  const rpc = client(), query = options(detail, rpc);
  assert.equal(query.enabled, true);
  assert.equal(await query.queryFn(), "ready");
  assert.equal(rpc.reads.length, 4);
  for (const read of rpc.reads) { assert.equal(read.blockNumber, 7n); assert.equal(read.address, detail.contractAddress); }
  assert.equal(query.refetchInterval, 5000);
});
test("wrong networks, failed RPC, unavailable blocks and reorgs fail conservatively", async () => {
  for (const rpc of [
    client({ getChainId: async () => 1 }),
    client({ getBlock: async () => { throw new Error("RPC offline"); } }),
    client({ getBlock: async () => ({ number: null, timestamp: state.timestamp }) }),
    client({ getBlock: async (request) => ({ number: 7n, hash: request.blockNumber ? "replacement" : "original", timestamp: state.timestamp }) }),
  ]) await assert.rejects(options(detail, rpc).queryFn());
  globalThis.readinessQueryState = { data: "ready", isError: true };
  const result = inspectReadinessQuery(detail);
  assert.equal(result.ready, false);
  assert.equal(result.status, "unavailable");
});
test("missing contract, unsupported chain, closed case and invalid index keep the query disabled", () => {
  for (const changes of [{ contractAddress: null }, { chainId: 1 }, { onChainMilestoneIndex: -1 }, { case: { ...detail.case, status: "Settled" } }]) {
    const source = { ...detail, ...changes };
    assert.equal(options(source).enabled, false);
    globalThis.readinessQueryState = { data: "ready", isError: false };
    assert.equal(inspectReadinessQuery(source).ready, false);
  }
});
