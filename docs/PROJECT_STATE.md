# TrustFlowAI project state

Last inspected: 2026-09-05. Worktree was clean on `main` at inspection start.

## Implemented

### Backend

- .NET 10 controller-based API with EF Core/PostgreSQL, ASP.NET Identity (GUID keys), JWT access tokens, rotating hashed refresh tokens, role authorization, CORS, rate limiting, ProblemDetails, and development OpenAPI/Scalar.
- Auth, public marketplace/project CRUD and dashboards, proposal create/accept/reject/withdraw, milestone create/edit/delete/review plus sequential execution, and client/freelancer workspaces are implemented.
- Wallet ownership uses a five-minute, one-time signed Ethereum challenge; verified wallet addresses are unique per account.
- A client can create and both project participants can read one escrow metadata record per in-progress project. Creation requires both verified wallets, snapshots chain/token/wallets/budget, and starts at `PendingDeployment`.
- Thirteen migrations and 10 PostgreSQL-backed integration test flows exist. Tests require an isolated `TRUSTFLOW_TEST_DB`.
- Runtime requires `ConnectionStrings__DefaultConnection` and a 32+ byte `Jwt__Key`. Only the `Testing` environment migrates automatically; development/production migrations must be applied explicitly.
- Blockchain deployment integration is implemented end to end. `Nethereum.Web3` sits alongside the existing `Nethereum.Signer`; `Options/BlockchainOptions.cs` (`ChainId`, `RpcUrl`, `DeployerPrivateKey`) is bound and validated at startup like `JwtSettings`, with `ChainId`/`RpcUrl` in `appsettings.Development.json` (local Hardhat, chain 31337) and `DeployerPrivateKey` required via user-secrets/env (`Blockchain__DeployerPrivateKey`), never committed. A trimmed, backend-owned copy of the compiled `TrustFlowEscrow` ABI/bytecode lives at `backend/TrustFlow.Api/Blockchain/Artifacts/TrustFlowEscrow.json` (copied to the build output; regenerate manually from `blockchain/artifacts/contracts/TrustFlowEscrow.sol/TrustFlowEscrow.json` after contract changes — no build-time dependency on the `blockchain/` project). `TokenAmountConverter` does exact decimal-to-base-units conversion and rejects inexact ones; `EscrowChainDeployer` deploys `TrustFlowEscrow` via Nethereum and reads a token's `decimals()` (its gas-estimation and `uint256[]` constructor-array encoding were verified against a real local Hardhat chain, not just docs — Nethereum does not auto-estimate gas, and `SendRequestAndWaitForReceiptAsync` needs an explicit `HexBigInteger` gas argument or it silently uses the base 21000 and reverts). `EscrowDeploymentOrchestrator` loads a `PendingDeployment` escrow and its milestones ordered by `SequenceNumber`, validates both wallets are set and the milestone sum equals the escrow total, converts amounts, deploys, and persists `ContractAddress`/`DeploymentTransactionHash`/`DeployedAt` while advancing status to `AwaitingFunding`; a new transient `EscrowStatus.Deploying` value plus an `ExecuteUpdateAsync` atomic claim (not tracked-entity mutation, to avoid an EF identity-map hazard when the controller and orchestrator share one scoped `DbContext`) stop concurrent triggers from double-deploying and roll the status back to `PendingDeployment` if deployment throws. `POST /api/projects/{projectId}/escrow/deploy` (client-only) triggers it. Funding sync is also implemented: `EscrowChainDeployer.GetEscrowSnapshotAsync` reads a deployed contract's `token`/`client`/`freelancer`/`totalAmount`/`escrowState` (verified end to end against a real local chain, including an actual `mint` -> `approve` -> `fund()` sequence); `EscrowFundingSyncOrchestrator` reads an `AwaitingFunding` escrow's on-chain snapshot, cross-checks it against the backend's own token/wallet/total values (throwing on mismatch), and advances `AwaitingFunding -> Funded` plus `FundedAt` via `POST /api/projects/{projectId}/escrow/sync` (either project party, since it only reads chain state and costs no gas). `MilestonesController.StartMilestone` now also requires `Project.Escrow.Status == Funded`, with a dedicated conflict message when it is not. `dotnet build` passes and the full integration suite — 16/16, including 6 escrow/funding-gate tests against a fake `IEscrowChainDeployer` registered only in `CustomWebApplicationFactory` — passed against a throwaway Postgres container on 2026-09-05 (the existing full-lifecycle `MilestoneWorkflowTests` test was updated to create/deploy/sync a real escrow before starting milestones, since that is now required). `FundingTransactionHash` is intentionally left unpopulated by the sync (no on-chain event-log lookup yet) — a known, minor gap, not a correctness issue.

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
- Re-verified on 2026-09-05: `MockUSDCModule.ts` and the parameterized `TrustFlowEscrowModule.ts` (no MockUSDC deployment or hardcoded client/freelancer/token/milestone values inside it) deployed cleanly against a fresh local node under an isolated deployment id, and the on-chain `token`/`client`/`freelancer`/`totalAmount`/milestone values read back via Viem matched `local-escrow.json` exactly. Temporary validation deployment journals were removed afterward; the reusable Ignition deployment layer needs no further changes.
- Source contains 12 escrow and 2 MockUSDC test cases. They were not rerun during this inspection; no committed test-result artifact proves the earlier passing claim.
- Contract test gaps include constructor guards, release/cancel authorization, invalid or repeated state paths, and exact-transfer rejection.

## Architecture and decisions to preserve

- PostgreSQL/backend state is off-chain workflow and indexing data; contract state is the source of truth for funds. Reconciliation must be idempotent and verify chain ID, receipts, bytecode/contract address, immutable participants/token, and ordered amounts before advancing backend status.
- Backend `decimal(18,2)` and frontend JavaScript `number` values are display/business units. Never pass them directly on-chain: define a decimal-string-to-`bigint` base-unit boundary and reject inexact conversion. MockUSDC uses 6 base-unit decimals.
- `PendingDeployment` and the transient `Deploying` (held only during an in-flight deployment attempt, via an atomic DB claim) exist only in the backend because no on-chain escrow exists yet at that point. On-chain lifecycle is `AwaitingFunding -> Funded -> Completed` or `Cancelled`; `workStarted` is a separate flag.
- Users sign fund/release/cancel/start transactions from their own wallets. The backend may prepare deployment data, read RPC state/events, and reconcile records, but must not hold user keys or make final financial decisions.
- No dispute/arbitration logic belongs in the current MVP. Milestones remain ordered and releasable once, one contract remains bound to one project, and cancellation remains pre-work only.

## Known issues and integration gap

- Backend can deploy an escrow's contract and sync its funding, advancing `PendingDeployment -> AwaitingFunding -> Funded` (see Backend implementation notes); `FundingTransactionHash` is not populated (no event-log lookup yet).
- Milestone start is now gated on a `Funded` escrow, but approval still does not release or verify on-chain payment: `releaseMilestone()` sync and cancellation-before-work-starts sync are not built yet.
- Proposal acceptance immediately sets `ProjectStatus.InProgress`, before escrow creation or funding. Integration must decide whether that means only "assigned" or whether the project lifecycle needs an awaiting-escrow state.
- Frontend has no wallet challenge/signature flow, verified-wallet display, escrow creation/funding, contract status, or transaction controls.
- Frontend requires `NEXT_PUBLIC_API_URL`, but no `.env.example` is committed.
- Cancellation exists in the contract and enums but has no backend/frontend workflow.
- `blockchain/README.md` and the frontend README are stale starter text. `readLocalEscrow.mjs` hardcodes a local RPC/address, sample `send-op-tx.ts` is unrelated, and local Ignition deployment output is tracked.

## Active milestone and exact next step

Active milestone: sync on-chain milestone release with the backend's approval workflow, before adding wallet/escrow UI.

Next step: when a client approves a submitted milestone, the client's own wallet must call the contract's `releaseMilestone(index)` (user-signed, not backend-signed); add backend verification of that release (correct milestone index, exact amount, ordered/one-time) to update `Escrow.ReleasedAmount` and the milestone's on-chain-release record, and only let `ApproveMilestone` complete the project once the contract itself reports `Completed`. Cancellation-before-work-starts sync and the frontend wallet/escrow UI remain later steps.

## Important paths

- Backend composition/data: `backend/TrustFlow.Api/Program.cs`, `backend/TrustFlow.Api/Data/AppDbContext.cs`
- Backend escrow/wallet: `backend/TrustFlow.Api/Controllers/EscrowsController.cs`, `backend/TrustFlow.Api/Controllers/WalletController.cs`, `backend/TrustFlow.Api/Models/Escrow.cs`
- Backend blockchain deployment/funding: `backend/TrustFlow.Api/Blockchain/EscrowChainDeployer.cs`, `backend/TrustFlow.Api/Blockchain/EscrowDeploymentOrchestrator.cs`, `backend/TrustFlow.Api/Blockchain/EscrowFundingSyncOrchestrator.cs`, `backend/TrustFlow.Api/Blockchain/TokenAmountConverter.cs`, `backend/TrustFlow.Api/Blockchain/Artifacts/TrustFlowEscrow.json`, `backend/TrustFlow.Api/Options/BlockchainOptions.cs`, `backend/TrustFlow.Api/Controllers/MilestonesController.cs`
- Backend tests: `backend/TrustFlow.Api.IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs`, `backend/TrustFlow.Api.IntegrationTests/Infrastructure/FakeEscrowChainDeployer.cs`, `backend/TrustFlow.Api.IntegrationTests/Tests/`
- Frontend composition/API/state: `frontend/trustflow-web/src/app/`, `frontend/trustflow-web/src/features/`, `frontend/trustflow-web/src/lib/api/api-client.ts`, `frontend/trustflow-web/src/stores/auth-store.ts`, `frontend/trustflow-web/src/config/env.ts`
- Contract/spec/tests: `blockchain/contracts/TrustFlowEscrow.sol`, `blockchain/specs/TrustFlowEscrow.md`, `blockchain/test/TrustFlowEscrow.ts`
- Deployment/read flow: `blockchain/ignition/modules/TrustFlowEscrowModule.ts`, `blockchain/ignition/modules/TrustFlowLocalModule.ts`, `blockchain/ignition/modules/parameters/local-escrow.json`, `blockchain/scripts/readLocalEscrow.mjs`
