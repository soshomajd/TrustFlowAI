# TrustFlow escrow contracts

Hardhat 3 with viem. Each freelancer on a project has one non-upgradeable escrow with fixed participants, token and milestone amounts. The contract owns payment truth; user wallets fund, start, release and cancel. The backend deployment account does not sign for either participant.

## Build and test

From this directory:

```bash
npm ci
npx hardhat compile
npx hardhat test nodejs
npx tsc --noEmit
```

`TrustFlowEscrow` preserves V1 behavior. New application escrows use `TrustFlowEscrowV2`, which automatically takes `floor(gross / 10)` in integer token base units from each freelancer award. Client refunds have no fee. The rate and receiving wallet cannot be changed after deployment. See [the contract specification](specs/TrustFlowEscrow.md).

## Local development

Start the disposable local network in one terminal:

```bash
npx hardhat node
```

In another terminal, deploy the test token with a new local deployment ID:

```bash
npx hardhat ignition deploy ignition/modules/MockUSDCModule.ts --network localhost --deployment-id local-demo-unique
```

RPC: `http://127.0.0.1:8545`; chain ID: `31337`. MockUSDC has 6 decimals and is only for local/test use. The API normally deploys an escrow after the client creates its metadata in the app. Its public arbiter and fee recipient settings must be configured first. The explicit V2 Ignition module requires token, client, freelancer, arbiter, milestone base-unit amounts and fee recipient parameters; it supplies no participant defaults.

A node restart removes contracts, balances and transaction receipts, while PostgreSQL keeps old escrow records. Restore MockUSDC, then use a fresh project for a new walkthrough. Keep old financial records intact. Local deployment journals are disposable and ignored for new `local-*` deployments.

## Read an escrow

```bash
node scripts/readLocalEscrow.mjs <escrow-address>
```

The address is required; there is no stale sample contract default. Set `TRUSTFLOW_RPC_URL` to change the read-only RPC, or `TRUSTFLOW_ESCROW_ADDRESS` instead of the argument. The script reads token decimals on-chain and reports gross awarded amounts. It refuses an address without bytecode.

Public deployment is deferred until the project is complete. The separate [Sepolia guide](../docs/SEPOLIA_TESTNET.md) is a future checklist, not evidence of a public deployment.
