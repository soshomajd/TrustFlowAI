# TrustFlowAI project state

Last inspected: 2026-08-24. Worktree was clean on `main` at inspection start.

## Implemented

### Backend

- .NET 10 controller-based API with EF Core/PostgreSQL, ASP.NET Identity (GUID keys), JWT access tokens, rotating hashed refresh tokens, role authorization, CORS, rate limiting, ProblemDetails, and development OpenAPI/Scalar.
- Auth, public marketplace/project CRUD and dashboards, proposal create/accept/reject/withdraw, milestone create/edit/delete/review plus sequential execution, and client/freelancer workspaces are implemented.
- Wallet ownership uses a five-minute, one-time signed Ethereum challenge; verified wallet addresses are unique per account.
- A client can create and both project participants can read one escrow metadata record per in-progress project. Creation requires both verified wallets, snapshots chain/token/wallets/budget, and starts at `PendingDeployment`.
- Thirteen migrations and 10 PostgreSQL-backed integration test flows exist. Tests require an isolated `TRUSTFLOW_TEST_DB`.
- Runtime requires `ConnectionStrings__DefaultConnection` and a 32+ byte `Jwt__Key`. Only the `Testing` environment migrates automatically; development/production migrations must be applied explicitly.

### Frontend

- Next.js 16 App Router marketplace UI with login/register/session restoration, role guards, client and freelancer dashboards, project CRUD/search, proposals, and milestone create/review/start/submit flows.
- Feature slices use Axios, TanStack Query, Zustand, React Hook Form/Zod, Tailwind 4, shadcn/Radix, and Motion. Access tokens remain memory-only; refresh uses credentials and a single retry after a 401.
- No wallet or escrow feature exists yet, and there is no Viem/Wagmi dependency. Frontend lint and TypeScript checks passed on 2026-08-24; there is no frontend test suite.

### Blockchain

- Hardhat 3.12, Viem, OpenZeppelin 5.6.1, Solidity 0.8.36, and EVM target `osaka`; the production compiler profile uses optimizer runs 200.
- `MockUSDC` is an unrestricted local/test token with 6 decimals.
- `TrustFlowEscrow` is non-upgradeable and project-scoped, with immutable token/client/freelancer/total and fixed milestone amounts. It enforces exact complete-budget funding, freelancer-only work start, sequential client-only one-time releases, completion, and client cancellation/refund only before work starts. It uses `SafeERC20` and `ReentrancyGuard` and has no admin, custody, dispute, or arbitration path.
- The local Ignition flow deploys MockUSDC, mints 2,000 mUSDC to the client, and deploys 500/800/700 mUSDC milestones. A Viem script reads the deployed escrow over local RPC.
- `TrustFlowEscrowModule.ts` now requires Ignition parameters for token, client, freelancer, and ordered milestone base-unit amounts; `local-escrow.json` supplies dev-only Hardhat values and targets the matching module ID. Deploy `MockUSDCModule.ts` first on a fresh local node so the configured token address exists.
- The parameterized module compiled, typechecked, and deployed successfully on local chain 31337 on 2026-08-24; temporary validation deployment journals were removed afterward.
- Source contains 12 escrow and 2 MockUSDC test cases. They were not rerun during this inspection; no committed test-result artifact proves the earlier passing claim.
- Contract test gaps include constructor guards, release/cancel authorization, invalid or repeated state paths, and exact-transfer rejection.

## Architecture and decisions to preserve

- PostgreSQL/backend state is off-chain workflow and indexing data; contract state is the source of truth for funds. Reconciliation must be idempotent and verify chain ID, receipts, bytecode/contract address, immutable participants/token, and ordered amounts before advancing backend status.
- Backend `decimal(18,2)` and frontend JavaScript `number` values are display/business units. Never pass them directly on-chain: define a decimal-string-to-`bigint` base-unit boundary and reject inexact conversion. MockUSDC uses 6 base-unit decimals.
- `PendingDeployment` exists only in the backend because no on-chain escrow exists yet. On-chain lifecycle is `AwaitingFunding -> Funded -> Completed` or `Cancelled`; `workStarted` is a separate flag.
- Users sign fund/release/cancel/start transactions from their own wallets. The backend may prepare deployment data, read RPC state/events, and reconcile records, but must not hold user keys or make final financial decisions.
- No dispute/arbitration logic belongs in the current MVP. Milestones remain ordered and releasable once, one contract remains bound to one project, and cancellation remains pre-work only.

## Known issues and integration gap

- Backend escrow is metadata only: there is no RPC/deployment/event service or status-transition endpoint. Contract/hash/timestamp fields never advance beyond creation.
- Backend milestones can currently start and complete without a funded escrow; approval does not release or verify on-chain payment. This does not yet enforce the intended deposit-before-work lifecycle.
- Proposal acceptance immediately sets `ProjectStatus.InProgress`, before escrow creation or funding. Integration must decide whether that means only "assigned" or whether the project lifecycle needs an awaiting-escrow state.
- Frontend has no wallet challenge/signature flow, verified-wallet display, escrow creation/funding, contract status, or transaction controls.
- Frontend requires `NEXT_PUBLIC_API_URL`, but no `.env.example` is committed.
- Cancellation exists in the contract and enums but has no backend/frontend workflow.
- `blockchain/README.md` and the frontend README are stale starter text. `readLocalEscrow.mjs` hardcodes a local RPC/address, sample `send-op-tx.ts` is unrelated, and local Ignition deployment output is tracked.

## Active milestone and exact next step

Active milestone: connect the deployable escrow contract to the backend's non-custodial lifecycle before adding wallet/escrow UI.

Next step: implement the idempotent backend deployment/reconciliation boundary: define exact decimal-to-base-unit conversion, deploy from the persisted escrow snapshot, verify the receipt and immutable on-chain values, advance `PendingDeployment -> AwaitingFunding -> Funded`, and gate work on confirmed `Funded` state without signing user fund transactions.

## Important paths

- Backend composition/data: `backend/TrustFlow.Api/Program.cs`, `backend/TrustFlow.Api/Data/AppDbContext.cs`
- Backend escrow/wallet: `backend/TrustFlow.Api/Controllers/EscrowsController.cs`, `backend/TrustFlow.Api/Controllers/WalletController.cs`, `backend/TrustFlow.Api/Models/Escrow.cs`
- Backend tests: `backend/TrustFlow.Api.IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs`, `backend/TrustFlow.Api.IntegrationTests/Tests/`
- Frontend composition/API/state: `frontend/trustflow-web/src/app/`, `frontend/trustflow-web/src/features/`, `frontend/trustflow-web/src/lib/api/api-client.ts`, `frontend/trustflow-web/src/stores/auth-store.ts`, `frontend/trustflow-web/src/config/env.ts`
- Contract/spec/tests: `blockchain/contracts/TrustFlowEscrow.sol`, `blockchain/specs/TrustFlowEscrow.md`, `blockchain/test/TrustFlowEscrow.ts`
- Deployment/read flow: `blockchain/ignition/modules/TrustFlowEscrowModule.ts`, `blockchain/ignition/modules/TrustFlowLocalModule.ts`, `blockchain/ignition/modules/parameters/local-escrow.json`, `blockchain/scripts/readLocalEscrow.mjs`
