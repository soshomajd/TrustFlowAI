import { buildModule } from "@nomicfoundation/hardhat-ignition/modules";

export default buildModule("TrustFlowLocalModule", (m) => {
  const client = m.getAccount(1);

  const freelancer = m.getAccount(2);

  const arbiter = m.getAccount(3);

  const milestoneAmounts = [500_000_000n, 800_000_000n, 700_000_000n];

  const totalAmount = 2_000_000_000n;

  const mockUSDC = m.contract("MockUSDC");

  m.call(mockUSDC, "mint", [client, totalAmount]);

  const escrow = m.contract("TrustFlowEscrow", [
    mockUSDC,
    client,
    freelancer,
    arbiter,
    milestoneAmounts,
  ]);

  return {
    mockUSDC,
    escrow,
  };
});
