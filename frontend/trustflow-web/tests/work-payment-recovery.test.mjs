import assert from "node:assert/strict";
import { test } from "node:test";

import { startWorkWithRecovery, releaseMilestoneWithRecovery } from "../src/features/escrow/lib/recoverable-escrow-actions.ts";
import { createTransactionConfirmer } from "../src/features/escrow/lib/transaction-confirmation.ts";

const hash = `0x${"2".repeat(64)}`;

// The same interruptions must be safe for freelancer work-start and client payment.
for (const [name, run, readKey, sendKey, resultKey] of [
  ["work start", startWorkWithRecovery, "readStarted", "start", "alreadyStarted"],
  ["milestone release", releaseMilestoneWithRecovery, "readReleased", "release", "alreadyReleased"],
]) {
  test(`${name}: a prior confirmation needs no second signature`, async () => {
    const result = await run({
      [readKey]: async () => true,
      [sendKey]: async () => assert.fail("must not send again"),
    });
    assert.equal(result[resultKey], true);
  });

  test(`${name}: rejected wallet request leaves the action retryable`, async () => {
    let applied = false;
    await assert.rejects(run({
      [readKey]: async () => applied,
      [sendKey]: async () => { throw new Error("User rejected"); },
    }), /User rejected/);
    const result = await run({
      [readKey]: async () => applied,
      [sendKey]: async () => { applied = true; },
    });
    assert.equal(result[resultKey], false);
  });

  test(`${name}: unknown chain state cannot reach the wallet`, async () => {
    await assert.rejects(run({
      [readKey]: async () => { throw new Error("RPC offline"); },
      [sendKey]: async () => assert.fail("must not sign while state is unknown"),
    }), /RPC offline/);
  });

  test(`${name}: a reverted receipt cannot report success`, async () => {
    const confirm = createTransactionConfirmer();
    await assert.rejects(run({
      [readKey]: async () => false,
      [sendKey]: () => confirm(name, async () => hash, async () => ({ status: "reverted" })),
    }), /reverted/);
  });

  test(`${name}: a successful replacement receipt without the action cannot report success`, async () => {
    await assert.rejects(run({
      [readKey]: async () => false,
      [sendKey]: async () => {},
    }), /not yet confirmed/);
  });

  test(`${name}: receipt interruption while pending waits on the same hash`, async () => {
    const confirm = createTransactionConfirmer();
    let applied = false;
    let sends = 0;
    let waits = 0;
    const actions = {
      [readKey]: async () => applied,
      [sendKey]: () => confirm(name, async () => { sends++; return hash; }, async (savedHash) => {
        assert.equal(savedHash, hash);
        if (++waits === 1) throw new Error("Lost receipt response");
        applied = true;
        return { status: "success" };
      }),
    };
    await assert.rejects(run(actions), /confirmation could not be read/);
    await run(actions);
    assert.equal(sends, 1);
  });

  test(`${name}: transaction mined but receipt response lost, retry reads state only`, async () => {
    const confirm = createTransactionConfirmer();
    let applied = false;
    let sends = 0;
    const actions = {
      [readKey]: async () => applied,
      [sendKey]: () => confirm(name, async () => { sends++; return hash; }, async () => {
        applied = true;
        throw new Error("Receipt response lost after mining");
      }),
    };
    await assert.rejects(run(actions), /confirmation could not be read/);
    const result = await run(actions);
    assert.equal(result[resultKey], true);
    assert.equal(sends, 1);
  });
}

test("approval API failure before or after commit never releases the payment twice", async () => {
  for (const committed of [false, true]) {
    let released = false;
    let transfers = 0;
    let approved = false;
    let credits = 0;
    let loseResponse = true;
    const release = () => releaseMilestoneWithRecovery({
      readReleased: async () => released,
      release: async () => { released = true; transfers++; },
    });
    const approve = async () => {
      await release();
      if (loseResponse && !committed) throw new Error("API offline before commit");
      if (!approved) { approved = true; credits++; }
      if (loseResponse) throw new Error("Approval response lost after commit");
      return { status: "Approved" };
    };
    await assert.rejects(approve(), /API offline|Approval response lost/);
    loseResponse = false;
    assert.equal((await approve()).status, "Approved");
    assert.equal(transfers, 1);
    assert.equal(credits, 1);
  }
});

test("a failed release cannot advance to off-chain approval", async () => {
  let approved = false;
  await assert.rejects((async () => {
    await releaseMilestoneWithRecovery({
      readReleased: async () => false,
      release: async () => { throw new Error("Payment reverted"); },
    });
    approved = true;
  })(), /Payment reverted/);
  assert.equal(approved, false);
});

test("work start saves its receipt after confirmation and recovers a failed sync without signing twice", async () => {
  let started = false, writes = 0, syncs = 0;
  const actions = {
    readStarted: async () => started,
    start: async () => { started = true; writes++; },
    sync: async () => { if (++syncs === 1) throw new Error("Lost API response"); },
  };
  await assert.rejects(startWorkWithRecovery(actions), /no new signature is needed/);
  assert.equal(writes, 1);
  assert.equal((await startWorkWithRecovery(actions)).alreadyStarted, true);
  assert.equal(writes, 1); assert.equal(syncs, 2);
});
