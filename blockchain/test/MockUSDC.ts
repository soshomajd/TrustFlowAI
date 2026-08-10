import assert from "node:assert/strict";
import { describe, it } from "node:test";

import { network } from "hardhat";
import { parseUnits } from "viem";

const { viem, networkHelpers } = await network.create();

describe("MockUSDC", function () {
  async function deployMockUSDCFixture() {
    const [deployer, client, freelancer] = await viem.getWalletClients();

    const token = await viem.deployContract("MockUSDC");

    const publicClient = await viem.getPublicClient();

    return {
      token,
      publicClient,
      deployer,
      client,
      freelancer,
    };
  }

  it("should have the correct token metadata", async function () {
    const { token } = await networkHelpers.loadFixture(deployMockUSDCFixture);

    assert.equal(await token.read.name(), "Mock USDC");

    assert.equal(await token.read.symbol(), "mUSDC");

    assert.equal(await token.read.decimals(), 6);
  });

  it("should mint tokens to the client wallet", async function () {
    const { token, publicClient, deployer, client } =
      await networkHelpers.loadFixture(deployMockUSDCFixture);

    const amount = parseUnits("2500", 6);

    const transactionHash = await token.write.mint(
      [client.account.address, amount],
      {
        account: deployer.account,
      },
    );

    await publicClient.waitForTransactionReceipt({
      hash: transactionHash,
    });

    const clientBalance = await token.read.balanceOf([client.account.address]);

    assert.equal(clientBalance, amount);
  });
});
