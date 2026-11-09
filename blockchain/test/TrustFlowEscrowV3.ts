import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { network } from "hardhat";
import { keccak256, stringToHex } from "viem";

const { viem, networkHelpers } = await network.create();
const ref = (text: string) => keccak256(stringToHex(text));
async function setup() {
  const [deployer, client, freelancer, arbiter, treasury, outsider] = await viem.getWalletClients();
  const publicClient = await viem.getPublicClient();
  const token = await viem.deployContract("MockUSDC");
  const escrow = await viem.deployContract("TrustFlowEscrowV3", [token.address,
    client.account.address, freelancer.account.address, arbiter.account.address,
    [100n], treasury.account.address, ref("first"), ref("first terms")]);
  async function mine(tx: Promise<`0x${string}`>) {
    assert.equal((await publicClient.waitForTransactionReceipt({ hash: await tx })).status, "success");
  }
  await mine(token.write.mint([client.account.address, 200n], { account: deployer.account }));
  async function fund() {
    await mine(token.write.approve([escrow.address, 200n], { account: client.account }));
    await mine(escrow.write.fund({ account: client.account }));
  }
  async function start() {
    await mine(escrow.write.acceptAndStart([ref("first"), ref("first terms")], { account: freelancer.account }));
  }
  return { token, escrow, client, freelancer, arbiter, treasury, outsider, mine, fund, start };
}

describe("funded hiring and appendable scope", () => {
  it("keeps two freelancers, deposits and later milestones in independent contracts", async () => {
    const f = await networkHelpers.loadFixture(setup);
    const second = await viem.deployContract("TrustFlowEscrowV3", [f.token.address,
      f.client.account.address, f.outsider.account.address, f.arbiter.account.address,
      [100n], f.treasury.account.address, ref("other worker"), ref("other terms")]);
    await f.fund();
    await f.mine(f.token.write.approve([second.address, 100n], { account: f.client.account }));
    await f.mine(second.write.fund({ account: f.client.account }));
    await viem.assertions.revertWithCustomError(f.escrow.write.acceptAndStart([ref("first"), ref("first terms")],
      { account: f.outsider.account }), f.escrow, "OnlyFreelancer");
    await f.start();
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    assert.equal(await f.token.read.balanceOf([f.freelancer.account.address]), 90n);
    assert.equal(await f.token.read.balanceOf([f.outsider.account.address]), 0n);
    assert.equal(await f.token.read.balanceOf([second.address]), 100n);
    assert.equal(await second.read.workStarted(), false);
    await f.mine(second.write.acceptAndStart([ref("other worker"), ref("other terms")], { account: f.outsider.account }));
    await f.mine(second.write.releaseMilestone([0n], { account: f.client.account }));
    assert.equal(await f.token.read.balanceOf([f.outsider.account.address]), 90n);
    assert.equal(await f.token.read.balanceOf([f.freelancer.account.address]), 90n);
    assert.equal(await f.token.read.balanceOf([f.treasury.account.address]), 20n);
    await f.mine(f.token.write.mint([f.client.account.address, 100n]));
    await f.mine(f.escrow.write.appendAndFundMilestone([100n, ref("same first worker"), ref("new terms")], { account: f.client.account }));
    assert.equal(await f.token.read.balanceOf([f.escrow.address]), 100n);
    assert.equal(await second.read.getMilestoneCount(), 1n);
    assert.equal(await f.escrow.read.getMilestoneCount(), 2n);
    assert.equal(await f.token.read.balanceOf([second.address]), 0n);
  });
  it("records no deposit before funding and only lets the freelancer accept the exact funded terms", async () => {
    const f = await networkHelpers.loadFixture(setup);
    assert.equal(await f.escrow.read.totalAmount(), 0n);
    await viem.assertions.revertWithCustomError(f.escrow.write.acceptAndStart([ref("first"), ref("first terms")],
      { account: f.freelancer.account }), f.escrow, "InvalidEscrowState");
    await f.fund();
    assert.equal(await f.escrow.read.totalAmount(), 100n);
    assert.equal(await f.escrow.read.workStarted(), false);
    await viem.assertions.revertWithCustomError(f.escrow.write.acceptAndStart([ref("first"), ref("wrong terms")],
      { account: f.freelancer.account }), f.escrow, "ScopeMismatch");
    await viem.assertions.revertWithCustomError(f.escrow.write.acceptAndStart([ref("first"), ref("first terms")],
      { account: f.client.account }), f.escrow, "OnlyFreelancer");
    await f.start();
    assert.equal(await f.escrow.read.workStarted(), true);
  });

  it("funds, accepts, pays 90/10, appends to the same contract and requires fresh consent", async () => {
    const f = await networkHelpers.loadFixture(setup);
    await f.fund();
    await f.start();
    await viem.assertions.revertWithCustomError(f.escrow.write.appendAndFundMilestone([100n, ref("second"), ref("second terms")],
      { account: f.client.account }), f.escrow, "InvalidEscrowState");
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    assert.equal(await f.token.read.balanceOf([f.freelancer.account.address]), 90n);
    assert.equal(await f.token.read.balanceOf([f.treasury.account.address]), 10n);
    await f.mine(f.escrow.write.appendAndFundMilestone([100n, ref("second"), ref("second terms")], { account: f.client.account }));
    assert.equal(await f.escrow.read.getMilestoneCount(), 2n);
    assert.equal(await f.escrow.read.totalAmount(), 200n);
    assert.equal(await f.escrow.read.workStarted(), false);
    await viem.assertions.revertWithCustomError(f.escrow.write.releaseMilestone([1n], { account: f.client.account }), f.escrow, "WorkNotStarted");
    await viem.assertions.revertWithCustomError(f.escrow.write.acceptAndStart([ref("first"), ref("first terms")],
      { account: f.freelancer.account }), f.escrow, "ScopeMismatch");
    await f.mine(f.escrow.write.acceptAndStart([ref("second"), ref("second terms")], { account: f.freelancer.account }));
    await f.mine(f.escrow.write.releaseMilestone([1n], { account: f.client.account }));
    assert.equal(await f.token.read.balanceOf([f.freelancer.account.address]), 180n);
    assert.equal(await f.token.read.balanceOf([f.treasury.account.address]), 20n);
    assert.equal(await f.escrow.read.remainingAmount(), 0n);
    await viem.assertions.revertWithCustomError(f.escrow.write.appendAndFundMilestone([1n, ref("second"), ref("terms")],
      { account: f.client.account }), f.escrow, "FundingReferenceAlreadyUsed");
  });

  it("rolls back an unfunded append completely and never credits token donations", async () => {
    const f = await networkHelpers.loadFixture(setup);
    await f.fund(); await f.start();
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    await assert.rejects(f.escrow.write.appendAndFundMilestone([101n, ref("large"), ref("terms")], { account: f.client.account }));
    assert.equal(await f.escrow.read.getMilestoneCount(), 1n);
    assert.equal(await f.escrow.read.usedFundingReferences([ref("large")]), false);
    assert.equal(await f.escrow.read.totalAmount(), 100n);
    await f.mine(f.token.write.transfer([f.escrow.address, 1n], { account: f.client.account }));
    assert.equal(await f.escrow.read.remainingAmount(), 0n);
  });

  it("refunds an unaccepted appended stage without undoing the earlier payment or charging a fee", async () => {
    const f = await networkHelpers.loadFixture(setup);
    await f.fund(); await f.start();
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    await f.mine(f.escrow.write.appendAndFundMilestone([100n, ref("second"), ref("terms")], { account: f.client.account }));
    await f.mine(f.escrow.write.cancelBeforeWorkStarts({ account: f.client.account }));
    assert.equal(await f.escrow.read.refundedAmount(), 100n);
    assert.equal(await f.escrow.read.releasedAmount(), 100n);
    assert.equal(await f.escrow.read.platformFeesPaid(), 10n);
    assert.equal(await f.escrow.read.remainingAmount(), 0n);
    await assert.rejects(f.escrow.write.appendAndFundMilestone([1n, ref("third"), ref("terms")], { account: f.client.account }));
  });

  for (const end of [false, true]) {
    it(`keeps 14-day arbitration, exact 20/80 split and end=${end}`, async () => {
      const f = await networkHelpers.loadFixture(setup);
      await f.fund(); await f.start();
      await f.mine(f.escrow.write.requestPayment({ account: f.freelancer.account }));
      await viem.assertions.revertWithCustomError(f.escrow.write.settleCase([0n, 20n, end], { account: f.arbiter.account }),
        f.escrow, "ResponseWindowNotElapsed");
      const open = await f.escrow.read.getOpenCase();
      await assert.rejects(f.escrow.write.appendAndFundMilestone([1n, ref("bad"), ref("terms")], { account: f.client.account }));
      assert.deepEqual(await f.escrow.read.getOpenCase(), open);
      await networkHelpers.time.increase(14 * 86400);
      await f.mine(f.escrow.write.settleCase([0n, 20n, end], { account: f.arbiter.account }));
      assert.equal(await f.token.read.balanceOf([f.freelancer.account.address]), 18n);
      assert.equal(await f.token.read.balanceOf([f.treasury.account.address]), 2n);
      assert.equal(await f.escrow.read.refundedAmount(), 80n);
      assert.equal(await f.escrow.read.escrowState(), end ? 4 : 2);
    });
  }

  it("permits the 90-day fallback and prevents the client releasing before acceptance", async () => {
    const f = await networkHelpers.loadFixture(setup);
    await f.fund();
    await viem.assertions.revertWithCustomError(f.escrow.write.releaseMilestone([0n], { account: f.client.account }), f.escrow, "WorkNotStarted");
    await f.start();
    await f.mine(f.escrow.write.requestRefund({ account: f.client.account }));
    await networkHelpers.time.increase(90 * 86400);
    await f.mine(f.escrow.write.settleStalledCase({ account: f.freelancer.account }));
    assert.equal(await f.escrow.read.releasedAmount(), 50n);
    assert.equal(await f.escrow.read.refundedAmount(), 50n);
    assert.equal(await f.escrow.read.platformFeesPaid(), 5n);
    assert.equal(await f.escrow.read.escrowState(), 4);
  });
});
