import { buildModule } from "@nomicfoundation/hardhat-ignition/modules";

export default buildModule("TrustFlowEscrowV2Module", (m) => {
  const token = m.getParameter<string>("tokenAddress");
  const client = m.getParameter<string>("clientAddress");
  const freelancer = m.getParameter<string>("freelancerAddress");
  const arbiter = m.getParameter<string>("arbiterAddress");
  const amounts = m.getParameter<bigint[]>("milestoneAmounts");
  const feeRecipient = m.getParameter<string>("feeRecipientAddress");
  const escrow = m.contract("TrustFlowEscrowV2", [token, client, freelancer, arbiter, amounts, feeRecipient]);
  return { escrow };
});
