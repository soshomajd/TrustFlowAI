# TrustFlow web client

Next.js 16 App Router frontend for the TrustFlow REST API. The backend enforces identity and marketplace permissions; contracts determine escrow funds. Wallet transactions stay user-signed.

## Local setup

Start PostgreSQL, ClamAV, the API and the local Hardhat network first. Deploy MockUSDC on a fresh node. From this directory:

```bash
npm ci
cp .env.example .env.local
npm run dev
```

Use `http://localhost:3000` consistently with the API at `http://localhost:5114/api`; the refresh-cookie and CORS settings depend on the origin. Access tokens stay in memory; the backend uses a credentialed refresh cookie. Do not add tokens or private keys to browser storage or public environment variables.

`.env.example` documents the local chain, token and site values. Every `NEXT_PUBLIC_*` value is public. Backend wallet secrets never belong here. Use a desktop browser with MetaMask or another supported injected wallet extension. Keep Client and Freelancer sessions in separate browser profiles for a two-role walkthrough.

## Verification

```bash
npm run lint
npx tsc --noEmit --incremental false
npm run build
```

The Client and Freelancer **How to get started** pages describe the current product flow. New escrows take a fixed 10% fee from gross freelancer awards; refunds remain fee-free, and V1 escrows retain their original terms. Past escrows remain visible after cancellation or resignation. Chat is plain text, project-scoped, and has a Load older messages action. Contact information is rejected by the API; files use the delivery flow.

A Hardhat restart removes on-chain deployments but leaves database history. Use a fresh project after restoring the test token; do not assume an old database address still contains a contract. Public deployment is currently deferred. See [project state](../../docs/PROJECT_STATE.md) for completed checks and remaining acceptance work.
