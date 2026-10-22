import assert from "node:assert/strict";
import { test } from "node:test";

import { fundEscrowWithRecovery, cancelEscrowWithRecovery } from "../src/features/escrow/lib/recoverable-escrow-actions.ts";
import { createTransactionConfirmer } from "../src/features/escrow/lib/transaction-confirmation.ts";

const amount = 100_000_000n;
const hash = `0x${"1".repeat(64)}`;

function fixture() {
  const chain = { state: 0, allowance: 0n, client: amount, escrow: 0n };
  const calls = { approve: 0, fund: 0, cancel: 0, sync: 0 };
  const actions = {
    readState: async () => chain.state,
    readFunding: async () => ({ amount, allowance: chain.allowance }),
    approve: async (value) => { calls.approve++; chain.allowance = value; },
    fund: async () => {
      calls.fund++;
      assert.equal(chain.state, 0);
      assert.ok(chain.allowance >= amount);
      chain.allowance -= amount;
      chain.client -= amount;
      chain.escrow += amount;
      chain.state = 1;
    },
    cancel: async () => {
      calls.cancel++;
      chain.client += chain.escrow;
      chain.escrow = 0n;
      chain.state = 3;
    },
    sync: async () => {
      calls.sync++;
      return { status: ["AwaitingFunding", "Funded", "Completed", "Cancelled", "Terminated"][chain.state] };
    },
  };
  return { chain, calls, actions };
}

test("API outage after funding: retry reconciles without another signature or transfer", async () => {
  const { chain, calls, actions } = fixture();
  const sync = actions.sync;
  actions.sync = async () => { throw new Error("API offline"); };
  await assert.rejects(fundEscrowWithRecovery(actions), /confirmed on-chain.*Refresh on-chain status/);
  assert.equal(chain.escrow, amount);
  actions.sync = sync;
  assert.equal((await fundEscrowWithRecovery(actions)).status, "Funded");
  assert.equal(chain.client, 0n);
  assert.deepEqual(calls, { approve: 1, fund: 1, cancel: 0, sync: 1 });
});

test("an existing approval is reused after funding is rejected", async () => {
  const { chain, calls, actions } = fixture();
  const fund = actions.fund;
  actions.fund = async () => { throw new Error("User rejected the request"); };
  await assert.rejects(fundEscrowWithRecovery(actions), /User rejected/);
  assert.equal(chain.client, amount);
  actions.fund = fund;
  await fundEscrowWithRecovery(actions);
  assert.equal(calls.approve, 1);
  assert.equal(calls.fund, 1);
});

test("rejected approval cannot advance to funding or API sync", async () => {
  const { chain, calls, actions } = fixture();
  actions.approve = async () => { throw new Error("User rejected"); };
  await assert.rejects(fundEscrowWithRecovery(actions), /User rejected/);
  assert.equal(chain.client, amount);
  assert.equal(calls.fund, 0);
  assert.equal(calls.sync, 0);
});

test("unreadable chain fails closed before asking the wallet to sign", async () => {
  const { calls, actions } = fixture();
  actions.readState = async () => { throw new Error("RPC unavailable"); };
  await assert.rejects(fundEscrowWithRecovery(actions), /RPC unavailable/);
  assert.deepEqual(calls, { approve: 0, fund: 0, cancel: 0, sync: 0 });
});

test("another tab cancels during approval: do not fund the cancelled escrow", async () => {
  const { chain, calls, actions } = fixture();
  actions.approve = async () => { chain.state = 3; };
  assert.equal((await fundEscrowWithRecovery(actions)).status, "Cancelled");
  assert.equal(calls.fund, 0);
  assert.equal(chain.client, amount);
});

test("API must not report funding success while still AwaitingFunding", async () => {
  const { actions } = fixture();
  actions.sync = async () => ({ status: "AwaitingFunding" });
  await assert.rejects(fundEscrowWithRecovery(actions), /not yet reflected/);
  await assert.rejects(fundEscrowWithRecovery(actions), /ahead of the site/);
});

test("API outage after refund: retry reconciles without another cancellation", async () => {
  const { chain, calls, actions } = fixture();
  await fundEscrowWithRecovery(actions);
  const sync = actions.sync;
  actions.sync = async () => { throw new Error("Lost API response"); };
  await assert.rejects(cancelEscrowWithRecovery(actions), /confirmed on-chain/);
  actions.sync = sync;
  assert.equal((await cancelEscrowWithRecovery(actions)).status, "Cancelled");
  assert.equal(calls.cancel, 1);
  assert.equal(chain.client, amount);
  assert.equal(chain.escrow, 0n);
});

test("completed and terminated escrows never request a cancellation signature", async () => {
  for (const state of [2, 4]) {
    const { chain, calls, actions } = fixture();
    chain.state = state;
    await assert.rejects(cancelEscrowWithRecovery(actions), /can no longer be cancelled/);
    assert.equal(calls.cancel, 0);
    await fundEscrowWithRecovery(actions);
    assert.equal(calls.approve, 0);
    assert.equal(calls.fund, 0);
  }
});

test("cancellation does not report success until the API confirms Cancelled", async () => {
  const { actions } = fixture();
  actions.sync = async () => ({ status: "Funded" });
  await assert.rejects(cancelEscrowWithRecovery(actions), /Cancellation is not yet reflected/);
});

test("receipt RPC interruption reuses the submitted hash instead of sending twice", async () => {
  const confirm = createTransactionConfirmer();
  let sends = 0;
  const send = async () => { sends++; return hash; };
  await assert.rejects(confirm("fund", send, async () => { throw new Error("RPC timeout"); }), /confirmation could not be read/);
  await confirm("fund", send, async (savedHash) => {
    assert.equal(savedHash, hash);
    return { status: "success" };
  });
  assert.equal(sends, 1);
});

test("funding retry waits for a known pending deposit instead of submitting another", async () => {
  const { chain, calls, actions } = fixture();
  const confirm = createTransactionConfirmer();
  const mine = actions.fund;
  let sends = 0;
  let waits = 0;
  actions.fund = () => confirm("fund", async () => {
    sends++;
    return hash;
  }, async () => {
    if (++waits === 1) throw new Error("RPC receipt timeout");
    await mine();
    return { status: "success" };
  });
  await assert.rejects(fundEscrowWithRecovery(actions), /confirmation could not be read/);
  assert.equal(chain.state, 0);
  assert.equal((await fundEscrowWithRecovery(actions)).status, "Funded");
  assert.equal(sends, 1);
  assert.equal(calls.approve, 1);
  assert.equal(chain.escrow, amount);
});

test("approval receipt timeout is recovered before sending the first deposit", async () => {
  const { calls, actions } = fixture();
  const confirm = createTransactionConfirmer();
  const mine = actions.approve;
  let sends = 0;
  let waits = 0;
  actions.approve = (value) => confirm("approve", async () => {
    sends++;
    return hash;
  }, async () => {
    if (++waits === 1) throw new Error("RPC receipt timeout");
    await mine(value);
    return { status: "success" };
  });
  await assert.rejects(fundEscrowWithRecovery(actions), /confirmation could not be read/);
  assert.equal(calls.fund, 0);
  await fundEscrowWithRecovery(actions);
  assert.equal(sends, 1);
  assert.equal(calls.fund, 1);
});

test("reverted approval cannot trigger a funding transaction, and can be retried", async () => {
  const confirm = createTransactionConfirmer();
  const { calls, actions } = fixture();
  let sends = 0;
  const send = async () => { sends++; return hash; };
  actions.approve = () => confirm("approve", send, async () => ({ status: "reverted" }));
  await assert.rejects(fundEscrowWithRecovery(actions), /reverted/);
  assert.equal(calls.fund, 0);
  await confirm("approve", send, async () => ({ status: "success" }));
  assert.equal(sends, 2);
});

test("concurrent attempts share one wallet request; different contracts remain independent", async () => {
  const confirm = createTransactionConfirmer();
  let sends = 0;
  let finish;
  const receipt = new Promise((resolve) => { finish = resolve; });
  const send = async () => { sends++; return hash; };
  const first = confirm("contract-a:fund", send, () => receipt);
  const second = confirm("contract-a:fund", send, () => receipt);
  assert.equal(first, second);
  await confirm("contract-b:fund", send, async () => ({ status: "success" }));
  finish({ status: "success" });
  await Promise.all([first, second]);
  assert.equal(sends, 2);
});
