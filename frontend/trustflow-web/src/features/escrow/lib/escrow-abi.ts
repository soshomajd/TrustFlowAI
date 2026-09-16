export const escrowAbi = [
  {
    type: "function",
    name: "fund",
    stateMutability: "nonpayable",
    inputs: [],
    outputs: [],
  },
  {
    type: "function",
    name: "startWork",
    stateMutability: "nonpayable",
    inputs: [],
    outputs: [],
  },
  {
    type: "function",
    name: "releaseMilestone",
    stateMutability: "nonpayable",
    inputs: [{ name: "milestoneIndex", type: "uint256" }],
    outputs: [],
  },
  {
    type: "function",
    name: "workStarted",
    stateMutability: "view",
    inputs: [],
    outputs: [{ name: "", type: "bool" }],
  },
  {
    type: "function",
    name: "escrowState",
    stateMutability: "view",
    inputs: [],
    outputs: [{ name: "", type: "uint8" }],
  },
  {
    type: "function",
    name: "getMilestone",
    stateMutability: "view",
    inputs: [{ name: "milestoneIndex", type: "uint256" }],
    outputs: [
      { name: "amount", type: "uint256" },
      { name: "released", type: "bool" },
    ],
  },
] as const;

export const onChainEscrowStateLabels = [
  "AwaitingFunding",
  "Funded",
  "Completed",
  "Cancelled",
] as const;
