import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { network } from "hardhat";
import { getAddress, parseUnits } from "viem";

const { viem, networkHelpers } = await network.create();

const milestoneAmounts = [
  parseUnits("500", 6),
  parseUnits("800", 6),
  parseUnits("700", 6),
];

const totalAmount = milestoneAmounts.reduce(
  (total, amount) => total + amount,
  0n,
);

async function deployEscrowFixture() {
  const [deployer, client, freelancer, outsider] =
    await viem.getWalletClients();

  const publicClient = await viem.getPublicClient();

  const token = await viem.deployContract("MockUSDC");

  const mintHash = await token.write.mint(
    [client.account.address, totalAmount],
    {
      account: deployer.account,
    },
  );

  await publicClient.waitForTransactionReceipt({
    hash: mintHash,
  });

  const escrow = await viem.deployContract("TrustFlowEscrow", [
    token.address,
    client.account.address,
    freelancer.account.address,
    milestoneAmounts,
  ]);

  return {
    token,
    escrow,
    publicClient,
    deployer,
    client,
    freelancer,
    outsider,
  };
}

type EscrowFixture = Awaited<ReturnType<typeof deployEscrowFixture>>;

async function fundEscrow(fixture: EscrowFixture) {
  const { token, escrow, publicClient, client } = fixture;

  const approveHash = await token.write.approve([escrow.address, totalAmount], {
    account: client.account,
  });

  await publicClient.waitForTransactionReceipt({
    hash: approveHash,
  });

  const fundHash = await escrow.write.fund({
    account: client.account,
  });

  await publicClient.waitForTransactionReceipt({
    hash: fundHash,
  });
}

async function startWork(fixture: EscrowFixture) {
  const { escrow, publicClient, freelancer } = fixture;

  const hash = await escrow.write.startWork({
    account: freelancer.account,
  });

  await publicClient.waitForTransactionReceipt({
    hash,
  });
}

describe("TrustFlowEscrow", function () {
  it("should deploy with the correct initial state", async function () {
    const { token, escrow, client, freelancer } =
      await networkHelpers.loadFixture(deployEscrowFixture);

    assert.equal(
      getAddress(await escrow.read.token()),
      getAddress(token.address),
    );

    assert.equal(
      getAddress(await escrow.read.client()),
      getAddress(client.account.address),
    );

    assert.equal(
      getAddress(await escrow.read.freelancer()),
      getAddress(freelancer.account.address),
    );

    assert.equal(await escrow.read.totalAmount(), totalAmount);

    assert.equal(await escrow.read.releasedAmount(), 0n);

    assert.equal(await escrow.read.nextMilestoneIndex(), 0n);

    assert.equal(await escrow.read.workStarted(), false);

    // AwaitingFunding = 0
    assert.equal(await escrow.read.escrowState(), 0);

    assert.equal(await escrow.read.getMilestoneCount(), 3n);

    assert.deepEqual(await escrow.read.getMilestone([0n]), [
      milestoneAmounts[0],
      false,
    ]);

    assert.deepEqual(await escrow.read.getMilestone([1n]), [
      milestoneAmounts[1],
      false,
    ]);

    assert.deepEqual(await escrow.read.getMilestone([2n]), [
      milestoneAmounts[2],
      false,
    ]);

    assert.equal(await escrow.read.remainingAmount(), totalAmount);
  });

  it("should require token approval before funding", async function () {
    const { token, escrow, client } =
      await networkHelpers.loadFixture(deployEscrowFixture);

    await viem.assertions.revertWithCustomErrorWithArgs(
      escrow.write.fund({
        account: client.account,
      }),
      token,
      "ERC20InsufficientAllowance",
      [getAddress(escrow.address), 0n, totalAmount],
    );

    // Transaction revert shode,
    // pas state bayad rollback shode bashe.
    assert.equal(await escrow.read.escrowState(), 0);
  });

  it("should allow only the client to fund", async function () {
    const { escrow, outsider } =
      await networkHelpers.loadFixture(deployEscrowFixture);

    await viem.assertions.revertWithCustomError(
      escrow.write.fund({
        account: outsider.account,
      }),
      escrow,
      "OnlyClient",
    );
  });

  it("should fund the complete project budget", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { token, escrow, client } = fixture;

    await fundEscrow(fixture);

    // Funded = 1
    assert.equal(await escrow.read.escrowState(), 1);

    assert.equal(await token.read.balanceOf([escrow.address]), totalAmount);

    assert.equal(await token.read.balanceOf([client.account.address]), 0n);

    assert.equal(await escrow.read.releasedAmount(), 0n);

    assert.equal(await escrow.read.remainingAmount(), totalAmount);
  });

  it("should allow only the freelancer to start work", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { escrow, client, freelancer } = fixture;

    await fundEscrow(fixture);

    await viem.assertions.revertWithCustomError(
      escrow.write.startWork({
        account: client.account,
      }),
      escrow,
      "OnlyFreelancer",
    );

    const hash = await escrow.write.startWork({
      account: freelancer.account,
    });

    await viem.assertions.emitWithArgs(hash, escrow, "WorkStarted", [
      freelancer.account.address,
    ]);

    assert.equal(await escrow.read.workStarted(), true);

    // State hanooz Funded ast.
    assert.equal(await escrow.read.escrowState(), 1);
  });

  it("should prevent milestone release before work starts", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { escrow, client } = fixture;

    await fundEscrow(fixture);

    await viem.assertions.revertWithCustomError(
      escrow.write.releaseMilestone([0n], {
        account: client.account,
      }),
      escrow,
      "WorkNotStarted",
    );
  });

  it("should enforce milestone order", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { escrow, client } = fixture;

    await fundEscrow(fixture);

    await startWork(fixture);

    await viem.assertions.revertWithCustomErrorWithArgs(
      escrow.write.releaseMilestone([1n], {
        account: client.account,
      }),
      escrow,
      "UnexpectedMilestoneIndex",
      [0n, 1n],
    );

    assert.equal(await escrow.read.nextMilestoneIndex(), 0n);
  });

  it("should release a milestone only once", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { escrow, publicClient, client } = fixture;

    await fundEscrow(fixture);

    await startWork(fixture);

    const hash = await escrow.write.releaseMilestone([0n], {
      account: client.account,
    });

    await publicClient.waitForTransactionReceipt({
      hash,
    });

    assert.deepEqual(await escrow.read.getMilestone([0n]), [
      milestoneAmounts[0],
      true,
    ]);

    assert.equal(await escrow.read.nextMilestoneIndex(), 1n);

    await viem.assertions.revertWithCustomErrorWithArgs(
      escrow.write.releaseMilestone([0n], {
        account: client.account,
      }),
      escrow,
      "MilestoneAlreadyReleased",
      [0n],
    );
  });

  it("should pay the freelancer and complete after the final milestone", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { token, escrow, publicClient, client, freelancer } = fixture;

    await fundEscrow(fixture);

    await startWork(fixture);

    for (let i = 0; i < milestoneAmounts.length; i++) {
      const hash = await escrow.write.releaseMilestone([BigInt(i)], {
        account: client.account,
      });

      await publicClient.waitForTransactionReceipt({
        hash,
      });
    }

    assert.equal(
      await token.read.balanceOf([freelancer.account.address]),
      totalAmount,
    );

    assert.equal(await token.read.balanceOf([escrow.address]), 0n);

    assert.equal(await escrow.read.releasedAmount(), totalAmount);

    assert.equal(await escrow.read.remainingAmount(), 0n);

    assert.equal(await escrow.read.nextMilestoneIndex(), 3n);

    // Completed = 2
    assert.equal(await escrow.read.escrowState(), 2);

    for (let i = 0; i < milestoneAmounts.length; i++) {
      const milestone = await escrow.read.getMilestone([BigInt(i)]);

      assert.equal(milestone[1], true);
    }
  });

  it("should cancel before funding", async function () {
    const { escrow, client } =
      await networkHelpers.loadFixture(deployEscrowFixture);

    const hash = await escrow.write.cancelBeforeWorkStarts({
      account: client.account,
    });

    await viem.assertions.emitWithArgs(hash, escrow, "EscrowCancelled", [0n]);

    // Cancelled = 3
    assert.equal(await escrow.read.escrowState(), 3);
  });

  it("should refund the client when cancelling after funding", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { token, escrow, publicClient, client } = fixture;

    await fundEscrow(fixture);

    const hash = await escrow.write.cancelBeforeWorkStarts({
      account: client.account,
    });

    await publicClient.waitForTransactionReceipt({
      hash,
    });

    assert.equal(
      await token.read.balanceOf([client.account.address]),
      totalAmount,
    );

    assert.equal(await token.read.balanceOf([escrow.address]), 0n);

    // Cancelled = 3
    assert.equal(await escrow.read.escrowState(), 3);
  });

  it("should not allow cancellation after work starts", async function () {
    const fixture = await networkHelpers.loadFixture(deployEscrowFixture);

    const { escrow, client } = fixture;

    await fundEscrow(fixture);

    await startWork(fixture);

    await viem.assertions.revertWithCustomError(
      escrow.write.cancelBeforeWorkStarts({
        account: client.account,
      }),
      escrow,
      "CancellationNotAllowedAfterWorkStarted",
    );

    // Revert shode, pas state hanooz Funded ast.
    assert.equal(await escrow.read.escrowState(), 1);

    assert.equal(await escrow.read.workStarted(), true);
  });
});
