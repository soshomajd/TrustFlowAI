import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { network } from "hardhat";
import { getAddress, zeroAddress } from "viem";
const { viem, networkHelpers } = await network.create();
const amounts = [101n, 19n];
async function deployFeeEscrow() {
  const [deployer, client, freelancer, arbiter, treasury, outsider] = await viem.getWalletClients();
  const publicClient = await viem.getPublicClient();
  const token = await viem.deployContract("MockUSDC");
  const escrow = await viem.deployContract("TrustFlowEscrowV2", [token.address, client.account.address,
    freelancer.account.address, arbiter.account.address, amounts, treasury.account.address]);
  async function mine(hash: Promise<`0x${string}`>) {
    await publicClient.waitForTransactionReceipt({ hash: await hash });
  }
  await mine(token.write.mint([client.account.address, 120n], { account: deployer.account }));
  async function fundAndStart() {
    await mine(token.write.approve([escrow.address, 120n], { account: client.account }));
    await mine(escrow.write.fund({ account: client.account }));
    await mine(escrow.write.startWork({ account: freelancer.account }));
  }
  async function assertBalances(net: bigint, fees: bigint, refunded: bigint, remaining: bigint) {
    assert.equal(await token.read.balanceOf([freelancer.account.address]), net);
    assert.equal(await token.read.balanceOf([treasury.account.address]), fees);
    assert.equal(await token.read.balanceOf([client.account.address]), refunded);
    assert.equal(await token.read.balanceOf([escrow.address]), remaining);
    assert.equal(await escrow.read.platformFeesPaid(), fees);
    assert.equal(await escrow.read.freelancerNetPaid(), net);
    assert.equal(await escrow.read.releasedAmount(), net + fees);
    assert.equal(net + fees + refunded + remaining, 120n);
  }
  return { token, escrow, client, freelancer, arbiter, treasury, outsider, mine, fundAndStart, assertBalances };
}
describe("TrustFlowEscrowV2 platform fee", () => {
  it("fixes the recipient and 10% policy at deployment and rejects zero recipient", async () => {
    const f = await networkHelpers.loadFixture(deployFeeEscrow);
    assert.equal(await f.escrow.read.feeRecipient(), getAddress(f.treasury.account.address));
    assert.equal(await f.escrow.read.PLATFORM_FEE_BPS(), 1000n);
    await viem.assertions.revertWithCustomError(viem.deployContract("TrustFlowEscrowV2", [f.token.address,
      f.client.account.address, f.freelancer.account.address, f.arbiter.account.address, amounts, zeroAddress]),
      f.escrow, "InvalidFeeRecipient");
  });
  it("pays net, floors per milestone, preserves gross accounting and never pays twice", async () => {
    const f = await networkHelpers.loadFixture(deployFeeEscrow);
    await f.fundAndStart();
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    await f.assertBalances(91n, 10n, 0n, 19n);
    assert.equal(await f.escrow.read.getMilestonePaid([0n]), 101n);
    assert.equal(await f.escrow.read.getMilestoneFee([0n]), 10n);
    await viem.assertions.revertWithCustomError(f.escrow.write.releaseMilestone([0n], { account: f.client.account }), f.escrow, "MilestoneAlreadyReleased");
    await f.mine(f.escrow.write.releaseMilestone([1n], { account: f.client.account }));
    await f.assertBalances(109n, 11n, 0n, 0n); // Not floor(120/10): each payout rounds separately.
    assert.equal(await f.escrow.read.escrowState(), 2);
  });
  for (const gross of [0n, 5n, 67n, 101n]) {
    it(`charges only the awarded ${gross} base units on settlement`, async () => {
      const f = await networkHelpers.loadFixture(deployFeeEscrow);
      await f.fundAndStart();
      await f.mine(f.escrow.write.requestPayment({ account: f.freelancer.account }));
      await networkHelpers.time.increase(14 * 86400);
      await viem.assertions.revertWithCustomError(f.escrow.write.settleCase([0n, gross, true], { account: f.outsider.account }), f.escrow, "OnlyArbiter");
      await f.mine(f.escrow.write.settleCase([0n, gross, true], { account: f.arbiter.account }));
      await f.assertBalances(gross - gross / 10n, gross / 10n, 120n - gross, 0n);
      await viem.assertions.revertWithCustomError(f.escrow.write.settleCase([0n, gross, true], { account: f.arbiter.account }), f.escrow, "NoOpenCase");
    });
  }
  it("charges only the freelancer half of the 90-day fallback", async () => {
    const f = await networkHelpers.loadFixture(deployFeeEscrow);
    await f.fundAndStart();
    await f.mine(f.escrow.write.requestRefund({ account: f.client.account }));
    await networkHelpers.time.increase(90 * 86400);
    await f.mine(f.escrow.write.settleStalledCase({ account: f.freelancer.account }));
    await f.assertBalances(45n, 5n, 70n, 0n);
  });
  it("takes no fee on pre-start cancellation", async () => {
    const f = await networkHelpers.loadFixture(deployFeeEscrow);
    await f.mine(f.token.write.approve([f.escrow.address, 120n], { account: f.client.account }));
    await f.mine(f.escrow.write.fund({ account: f.client.account }));
    await f.mine(f.escrow.write.cancelBeforeWorkStarts({ account: f.client.account }));
    await f.assertBalances(0n, 0n, 120n, 0n);
  });
  it("never fees the resignation refund or refunds fees paid earlier", async () => {
    const f = await networkHelpers.loadFixture(deployFeeEscrow);
    await f.fundAndStart();
    await f.mine(f.escrow.write.releaseMilestone([0n], { account: f.client.account }));
    await f.mine(f.escrow.write.resign({ account: f.freelancer.account }));
    await f.assertBalances(91n, 10n, 19n, 0n);
  });
});
