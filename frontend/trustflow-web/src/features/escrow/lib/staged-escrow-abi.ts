export const stagedEscrowAbi = [
  { type: "function", name: "fund", stateMutability: "nonpayable", inputs: [], outputs: [] },
  { type: "function", name: "appendAndFundMilestone", stateMutability: "nonpayable", inputs: [
    { name: "amount", type: "uint256" }, { name: "fundingReference", type: "bytes32" }, { name: "termsHash", type: "bytes32" },
  ], outputs: [] },
  { type: "function", name: "acceptAndStart", stateMutability: "nonpayable", inputs: [
    { name: "fundingReference", type: "bytes32" }, { name: "termsHash", type: "bytes32" },
  ], outputs: [] },
  { type: "function", name: "usedFundingReferences", stateMutability: "view", inputs: [
    { name: "reference", type: "bytes32" },
  ], outputs: [{ name: "", type: "bool" }] },
  { type: "function", name: "fundingReferences", stateMutability: "view", inputs: [
    { name: "index", type: "uint256" },
  ], outputs: [{ name: "", type: "bytes32" }] },
  { type: "function", name: "termsHashes", stateMutability: "view", inputs: [
    { name: "index", type: "uint256" },
  ], outputs: [{ name: "", type: "bytes32" }] },
] as const;
