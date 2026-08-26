import { buildModule } from "@nomicfoundation/hardhat-ignition/modules";

export default buildModule("TrustFlowEscrowModule", (m) => {
  const tokenAddress = m.getParameter<string>("tokenAddress");

  const clientAddress = m.getParameter<string>("clientAddress");

  const freelancerAddress = m.getParameter<string>("freelancerAddress");

  const milestoneAmounts = m.getParameter<bigint[]>("milestoneAmounts");

  const escrow = m.contract("TrustFlowEscrow", [
    tokenAddress,
    clientAddress,
    freelancerAddress,
    milestoneAmounts,
  ]);

  return {
    escrow,
  };
});
