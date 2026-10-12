import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { network } from "hardhat";
import { getAddress, parseUnits, zeroAddress } from "viem";

const { viem, networkHelpers } = await network.create();

const DAY = 24 * 60 * 60;
const RESPONSE_WINDOW = 14 * DAY;
const STALL_WINDOW = 90 * DAY;

const milestoneAmounts = [
  parseUnits("500", 6),
  parseUnits("800", 6),
  parseUnits("700", 6),
];

const totalAmount = milestoneAmounts.reduce((sum, amount) => sum + amount, 0n);

// CaseKind: None = 0, PaymentRequest = 1, RefundRequest = 2
// EscrowState: AwaitingFunding 0, Funded 1, Completed 2, Cancelled 3, Terminated 4

async function deployFundedFixture() {
  const [deployer, client, freelancer, outsider, arbiter] =
    await viem.getWalletClients();

  const publicClient = await viem.getPublicClient();
  const token = await viem.deployContract("MockUSDC");

  await publicClient.waitForTransactionReceipt({
    hash: await token.write.mint([client.account.address, totalAmount], {
      account: deployer.account,
    }),
  });

  const escrow = await viem.deployContract("TrustFlowEscrow", [
    token.address,
    client.account.address,
    freelancer.account.address,
    arbiter.account.address,
    milestoneAmounts,
  ]);

  await publicClient.waitForTransactionReceipt({
    hash: await token.write.approve([escrow.address, totalAmount], {
      account: client.account,
    }),
  });
  await publicClient.waitForTransactionReceipt({
    hash: await escrow.write.fund({ account: client.account }),
  });

  return { token, escrow, publicClient, deployer, client, freelancer, outsider, arbiter };
}

async function deployStartedFixture() {
  const fixture = await deployFundedFixture();
  await fixture.publicClient.waitForTransactionReceipt({
    hash: await fixture.escrow.write.startWork({
      account: fixture.freelancer.account,
    }),
  });
  return fixture;
}

type Fixture = Awaited<ReturnType<typeof deployStartedFixture>>;

async function send(fixture: Fixture, promise: Promise<`0x${string}`>) {
  await fixture.publicClient.waitForTransactionReceipt({ hash: await promise });
}

async function balances(fixture: Fixture) {
  const { token, escrow, client, freelancer } = fixture;
  return {
    escrow: await token.read.balanceOf([escrow.address]),
    client: await token.read.balanceOf([client.account.address]),
    freelancer: await token.read.balanceOf([freelancer.account.address]),
  };
}

describe("TrustFlowEscrow cases", function () {
  describe("constructor", function () {
    it("stores the arbiter", async function () {
      const { escrow, arbiter } =
        await networkHelpers.loadFixture(deployStartedFixture);
      assert.equal(
        getAddress(await escrow.read.arbiter()),
        getAddress(arbiter.account.address),
      );
    });

    it("rejects a zero arbiter and an arbiter who is a party", async function () {
      const [, client, freelancer, , arbiter] = await viem.getWalletClients();
      const token = await viem.deployContract("MockUSDC");

      const args = (arbiterAddress: `0x${string}`) =>
        [
          token.address,
          client.account.address,
          freelancer.account.address,
          arbiterAddress,
          milestoneAmounts,
        ] as const;

      // A reverting deployment has no decodable reason, so check that it
      // fails and that the same arguments with a valid arbiter succeed.
      for (const bad of [
        zeroAddress,
        client.account.address,
        freelancer.account.address,
      ]) {
        await assert.rejects(
          viem.deployContract("TrustFlowEscrow", args(bad)),
        );
      }

      await viem.deployContract("TrustFlowEscrow", args(arbiter.account.address));
    });
  });

  describe("opening a case", function () {
    it("lets the freelancer ask for payment once work has started", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, freelancer } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));

      const [kind, openedAt] = await escrow.read.getOpenCase();
      assert.equal(kind, 1);
      assert.ok(openedAt > 0n);
    });

    it("lets the client ask for a refund once work has started", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));

      assert.equal((await escrow.read.getOpenCase())[0], 2);
    });

    it("refuses before work has started", async function () {
      const { escrow, client, freelancer } =
        await networkHelpers.loadFixture(deployFundedFixture);

      await viem.assertions.revertWithCustomError(
        escrow.write.requestPayment({ account: freelancer.account }),
        escrow,
        "WorkNotStartedYet",
      );
      await viem.assertions.revertWithCustomError(
        escrow.write.requestRefund({ account: client.account }),
        escrow,
        "WorkNotStartedYet",
      );
    });

    it("keeps each request to its own side", async function () {
      const { escrow, client, freelancer, outsider, arbiter } =
        await networkHelpers.loadFixture(deployStartedFixture);

      await viem.assertions.revertWithCustomError(
        escrow.write.requestPayment({ account: client.account }),
        escrow,
        "OnlyFreelancer",
      );
      await viem.assertions.revertWithCustomError(
        escrow.write.requestRefund({ account: freelancer.account }),
        escrow,
        "OnlyClient",
      );
      for (const stranger of [outsider, arbiter]) {
        await viem.assertions.revertWithCustomError(
          escrow.write.requestPayment({ account: stranger.account }),
          escrow,
          "OnlyFreelancer",
        );
        await viem.assertions.revertWithCustomError(
          escrow.write.requestRefund({ account: stranger.account }),
          escrow,
          "OnlyClient",
        );
      }
    });

    it("allows one case at a time", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));

      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.requestRefund({ account: client.account }),
        escrow,
        "CaseAlreadyOpen",
        [1],
      );
      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.requestPayment({ account: freelancer.account }),
        escrow,
        "CaseAlreadyOpen",
        [1],
      );
    });

    it("can be withdrawn only by the side that opened it", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer, outsider } = fixture;

      await viem.assertions.revertWithCustomError(
        escrow.write.withdrawRequest({ account: freelancer.account }),
        escrow,
        "NoOpenCase",
      );

      await send(fixture, escrow.write.requestRefund({ account: client.account }));

      for (const other of [freelancer, outsider]) {
        await viem.assertions.revertWithCustomError(
          escrow.write.withdrawRequest({ account: other.account }),
          escrow,
          "OnlyCaseOpener",
        );
      }

      await send(fixture, escrow.write.withdrawRequest({ account: client.account }));
      assert.equal((await escrow.read.getOpenCase())[0], 0);
    });
  });

  describe("the client is silent", function () {
    it("lets the arbiter pay the freelancer only after 14 days", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, freelancer, arbiter } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));

      await networkHelpers.time.increase(RESPONSE_WINDOW - 60);

      await assert.rejects(
        escrow.write.settleCase([0n, milestoneAmounts[0], false], {
          account: arbiter.account,
        }),
        /ResponseWindowNotElapsed/,
      );

      await networkHelpers.time.increase(120);

      await send(
        fixture,
        escrow.write.settleCase([0n, milestoneAmounts[0], false], {
          account: arbiter.account,
        }),
      );

      const after = await balances(fixture);
      assert.equal(after.freelancer, milestoneAmounts[0]);
      assert.equal(after.client, 0n);
      assert.equal(after.escrow, totalAmount - milestoneAmounts[0]);

      assert.equal(await escrow.read.nextMilestoneIndex(), 1n);
      assert.equal(await escrow.read.releasedAmount(), milestoneAmounts[0]);
      assert.equal(await escrow.read.getMilestonePaid([0n]), milestoneAmounts[0]);
      assert.equal(await escrow.read.isSettledByCase([0n]), true);
      assert.equal((await escrow.read.getMilestone([0n]))[1], true);
      assert.equal((await escrow.read.getOpenCase())[0], 0);
      assert.equal(await escrow.read.escrowState(), 1);
    });
  });

  describe("the freelancer does not deliver", function () {
    it("lets the arbiter refund the milestone and end the escrow", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, arbiter } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      await send(
        fixture,
        escrow.write.settleCase([0n, 0n, true], { account: arbiter.account }),
      );

      const after = await balances(fixture);
      assert.equal(after.client, totalAmount);
      assert.equal(after.freelancer, 0n);
      assert.equal(after.escrow, 0n);
      assert.equal(await escrow.read.escrowState(), 4);
      assert.equal(await escrow.read.remainingAmount(), 0n);
      assert.equal(await escrow.read.getMilestonePaid([0n]), 0n);
    });

    it("keeps milestones that were already paid", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, arbiter } = fixture;

      await send(fixture, escrow.write.releaseMilestone([0n], { account: client.account }));
      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);
      await send(
        fixture,
        escrow.write.settleCase([1n, 0n, true], { account: arbiter.account }),
      );

      const after = await balances(fixture);
      assert.equal(after.freelancer, milestoneAmounts[0]);
      assert.equal(after.client, milestoneAmounts[1] + milestoneAmounts[2]);
      assert.equal(after.escrow, 0n);
      assert.equal(await escrow.read.escrowState(), 4);
    });

    it("can refund one milestone and let the job go on", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, arbiter } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);
      await send(
        fixture,
        escrow.write.settleCase([0n, 0n, false], { account: arbiter.account }),
      );

      assert.equal(await escrow.read.escrowState(), 1);
      assert.equal(await escrow.read.nextMilestoneIndex(), 1n);
      assert.equal((await balances(fixture)).client, milestoneAmounts[0]);
      assert.equal(
        await escrow.read.remainingAmount(),
        milestoneAmounts[1] + milestoneAmounts[2],
      );

      // The next milestone is released the normal way.
      await send(fixture, escrow.write.releaseMilestone([1n], { account: client.account }));
      assert.equal((await balances(fixture)).freelancer, milestoneAmounts[1]);
    });
  });

  describe("a split", function () {
    it("pays both sides and the amounts always add up", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, freelancer, arbiter } = fixture;

      const toFreelancer = 123_456_789n; // not a round share of 500 USDC
      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);
      await send(
        fixture,
        escrow.write.settleCase([0n, toFreelancer, false], {
          account: arbiter.account,
        }),
      );

      const after = await balances(fixture);
      assert.equal(after.freelancer, toFreelancer);
      assert.equal(after.client, milestoneAmounts[0] - toFreelancer);
      assert.equal(after.freelancer + after.client, milestoneAmounts[0]);
      assert.equal(after.escrow, totalAmount - milestoneAmounts[0]);
      assert.equal(await escrow.read.getMilestonePaid([0n]), toFreelancer);
    });

    it("completes the escrow when the last milestone is settled", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer, arbiter } = fixture;

      await send(fixture, escrow.write.releaseMilestone([0n], { account: client.account }));
      await send(fixture, escrow.write.releaseMilestone([1n], { account: client.account }));
      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);
      await send(
        fixture,
        escrow.write.settleCase([2n, milestoneAmounts[2] / 2n, false], {
          account: arbiter.account,
        }),
      );

      assert.equal(await escrow.read.escrowState(), 2);
      assert.equal((await balances(fixture)).escrow, 0n);
    });
  });

  describe("limits on the arbiter", function () {
    it("cannot act without an open case", async function () {
      const { escrow, arbiter } =
        await networkHelpers.loadFixture(deployStartedFixture);

      await viem.assertions.revertWithCustomError(
        escrow.write.settleCase([0n, 0n, false], { account: arbiter.account }),
        escrow,
        "NoOpenCase",
      );
    });

    it("can only settle the milestone that is next in line", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, arbiter } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.settleCase([1n, 0n, false], { account: arbiter.account }),
        escrow,
        "CaseMilestoneMismatch",
        [0n, 1n],
      );
    });

    it("cannot give the freelancer more than the milestone", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, freelancer, arbiter } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.settleCase([0n, milestoneAmounts[0] + 1n, false], {
          account: arbiter.account,
        }),
        escrow,
        "FreelancerAmountTooLarge",
        [milestoneAmounts[0], milestoneAmounts[0] + 1n],
      );
    });

    it("is the only one who can settle", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer, outsider } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      for (const other of [client, freelancer, outsider]) {
        await viem.assertions.revertWithCustomError(
          escrow.write.settleCase([0n, milestoneAmounts[0], false], {
            account: other.account,
          }),
          escrow,
          "OnlyArbiter",
        );
      }
    });

    it("cannot settle a case that was withdrawn", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, arbiter } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await send(fixture, escrow.write.withdrawRequest({ account: client.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      await viem.assertions.revertWithCustomError(
        escrow.write.settleCase([0n, 0n, true], { account: arbiter.account }),
        escrow,
        "NoOpenCase",
      );
    });

    it("cannot settle a milestone that was released meanwhile", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer, arbiter } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);
      await send(fixture, escrow.write.releaseMilestone([0n], { account: client.account }));

      // Releasing closed the case, so there is nothing left to settle.
      await viem.assertions.revertWithCustomError(
        escrow.write.settleCase([0n, 0n, true], { account: arbiter.account }),
        escrow,
        "NoOpenCase",
      );
    });

    it("never touches an escrow that is not funded", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer, arbiter } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await send(fixture, escrow.write.resign({ account: freelancer.account }));
      await networkHelpers.time.increase(RESPONSE_WINDOW + 1);

      await viem.assertions.revertWithCustomError(
        escrow.write.settleCase([0n, 0n, false], { account: arbiter.account }),
        escrow,
        "NoOpenCase",
      );
      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.requestRefund({ account: client.account }),
        escrow,
        "InvalidEscrowState",
        [1, 4],
      );
    });
  });

  describe("normal release stays in the client's hands", function () {
    it("lets the client release while a case is open, which closes it", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer } = fixture;

      await send(fixture, escrow.write.requestPayment({ account: freelancer.account }));
      await send(fixture, escrow.write.releaseMilestone([0n], { account: client.account }));

      assert.equal((await escrow.read.getOpenCase())[0], 0);
      assert.equal(await escrow.read.getMilestonePaid([0n]), milestoneAmounts[0]);
      assert.equal(await escrow.read.isSettledByCase([0n]), false);
      assert.equal((await balances(fixture)).freelancer, milestoneAmounts[0]);
    });
  });

  describe("the freelancer resigns", function () {
    it("hands everything unreleased back to the client", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer } = fixture;

      await send(fixture, escrow.write.releaseMilestone([0n], { account: client.account }));
      await send(fixture, escrow.write.resign({ account: freelancer.account }));

      const after = await balances(fixture);
      assert.equal(after.freelancer, milestoneAmounts[0]);
      assert.equal(after.client, milestoneAmounts[1] + milestoneAmounts[2]);
      assert.equal(after.escrow, 0n);
      assert.equal(await escrow.read.escrowState(), 4);
      assert.equal(await escrow.read.remainingAmount(), 0n);
    });

    it("is only for the freelancer and only after work has started", async function () {
      const started = await networkHelpers.loadFixture(deployStartedFixture);
      for (const other of [started.client, started.outsider, started.arbiter]) {
        await viem.assertions.revertWithCustomError(
          started.escrow.write.resign({ account: other.account }),
          started.escrow,
          "OnlyFreelancer",
        );
      }

      const funded = await networkHelpers.loadFixture(deployFundedFixture);
      await viem.assertions.revertWithCustomError(
        funded.escrow.write.resign({ account: funded.freelancer.account }),
        funded.escrow,
        "WorkNotStartedYet",
      );
    });

    it("cannot be done twice", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, freelancer } = fixture;

      await send(fixture, escrow.write.resign({ account: freelancer.account }));
      await viem.assertions.revertWithCustomErrorWithArgs(
        escrow.write.resign({ account: freelancer.account }),
        escrow,
        "InvalidEscrowState",
        [1, 4],
      );
    });
  });

  describe("the arbiter never answers", function () {
    it("splits the milestone in half after 90 days and returns the rest", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, freelancer } = fixture;

      await send(fixture, escrow.write.requestRefund({ account: client.account }));

      await networkHelpers.time.increase(STALL_WINDOW - 60);
      await assert.rejects(
        escrow.write.settleStalledCase({ account: freelancer.account }),
        /StallWindowNotElapsed/,
      );

      await networkHelpers.time.increase(120);
      await send(fixture, escrow.write.settleStalledCase({ account: freelancer.account }));

      const after = await balances(fixture);
      assert.equal(after.freelancer, milestoneAmounts[0] / 2n);
      assert.equal(
        after.client,
        totalAmount - milestoneAmounts[0] / 2n,
      );
      assert.equal(after.escrow, 0n);
      assert.equal(await escrow.read.escrowState(), 4);
    });

    it("is only for the two sides and needs an open case", async function () {
      const fixture = await networkHelpers.loadFixture(deployStartedFixture);
      const { escrow, client, outsider, arbiter } = fixture;

      await viem.assertions.revertWithCustomError(
        escrow.write.settleStalledCase({ account: client.account }),
        escrow,
        "NoOpenCase",
      );

      await send(fixture, escrow.write.requestRefund({ account: client.account }));
      await networkHelpers.time.increase(STALL_WINDOW + 1);

      for (const other of [outsider, arbiter]) {
        await viem.assertions.revertWithCustomError(
          escrow.write.settleStalledCase({ account: other.account }),
          escrow,
          "OnlyParticipant",
        );
      }
    });
  });

  describe("cancelling before work", function () {
    it("still refunds in full and keeps the books straight", async function () {
      const fixture = await networkHelpers.loadFixture(deployFundedFixture);
      const { escrow, client } = fixture;

      await send(fixture, escrow.write.cancelBeforeWorkStarts({ account: client.account }));

      assert.equal(await escrow.read.escrowState(), 3);
      assert.equal(await escrow.read.refundedAmount(), totalAmount);
      assert.equal(await escrow.read.remainingAmount(), 0n);
      assert.equal((await balances(fixture)).client, totalAmount);
    });
  });
});
