# Final-test readiness review — 2026-09-15

## Result and scope

The local MVP is ready for exploratory testing, but final acceptance should wait for the financial workflow recovery and state-consistency fixes below. Review covered repository documentation, the contract/spec/tests, backend controllers and chain orchestration, frontend authentication/wallet/escrow/milestone flows, and development configuration. Existing uncommitted work was preserved. This is a code and local runtime review, not a completed browser or wallet acceptance test.

## Verified in this review, before the reported system restart

- Backend solution build: passed, zero warnings/errors.
- Backend integration tests: 17 passed, zero failed/skipped, using `trustflow_review_tests` in a newly created local PostgreSQL container. Chain interactions in this suite use the existing fake deployer.
- Contract tests: 14 passed against Hardhat's isolated test network.
- Frontend TypeScript check and ESLint: passed.
- Next.js dev startup, backend Development startup, and explicit application of the 13 existing migrations to a separate development database: succeeded.
- HTTP checks: `/` on port 3000, `/api/projects` and `/scalar/v1` on port 5114 returned 200. These do not prove client hydration, authentication, or wallet interactions.
- Local Hardhat node started; MockUSDC deployed at `0x5FbDB2315678afecb367f032d93F642f64180aa3`.
- Browser connection discovery returned no available browsers; MetaMask and visual acceptance were not tested. Production frontend build was not repeated; its earlier result is recorded in PROJECT_STATE.md.

## Required before final acceptance

### 1. Verify on-chain work start before starting milestones

`backend/TrustFlow.Api/Controllers/MilestonesController.cs:624` checks the database escrow status `Funded`, but does not read `workStarted`. The freelancer workspace's `canStart` also has no on-chain start gate. A funded project can therefore start and submit a milestone while the contract still permits the client to cancel and refund. Require a verified on-chain start before advancing the off-chain workflow, and make the UI reflect that prerequisite. Cover funded-but-not-started and externally cancelled cases.

### 2. Recover approval after a successful release and a failed API call

`frontend/trustflow-web/src/features/milestones/components/client-milestone-review-actions.tsx` always runs `releaseMutation.mutateAsync` before API approval. If the release succeeds and the API call fails, another click tries to release the same milestone and reverts. Read the release state and allow reconciliation without another payment; retain transaction progress across reloads and check receipt success. Verify recovery after a timeout, reload, and a transaction submitted outside the app.

### 3. Make deployment recovery preserve one contract per project

`backend/TrustFlow.Api/Blockchain/EscrowDeploymentOrchestrator.cs` resets `Deploying` to `PendingDeployment` on every exception. A transaction can succeed while the following DB write fails; retry can deploy another contract. Process termination can also strand the row at `Deploying`. Persist a deployment attempt/transaction identity and reconcile uncertain outcomes before retrying. Test a DB failure after broadcast/confirmation and a process restart during deployment.

### 4. Complete cancellation and reconciliation

The contract supports `cancelBeforeWorkStarts`, but the app has no corresponding wallet action or cancellation sync. `EscrowFundingSyncOrchestrator` only transitions `AwaitingFunding` to `Funded`; it cannot reconcile a cancellation or an already-completed contract. Implement user-signed cancellation, verified backend cancellation/refund state, and repeated safe sync. Include cancellation before funding, after funding, and rejection after work starts.

### 5. Bind records and wallet actions to the configured chain

`EscrowsController.CreateEscrow` accepts any positive chain ID, while the deployer always uses global Blockchain options and the frontend only configures Hardhat. The persisted chain can disagree with the actual deployment. Validate the requested/configured/RPC chain identity, and use the escrow chain and immutable participant address for wallet action prerequisites. Contract identity/ordered-amount and receipt validation should be explicit during reconciliation. The earlier note that Sepolia requires only backend config changes is inaccurate: frontend chain and token constants also need changes.

### 6. Finish wallet and escrow UI state handling

- Reset verified-wallet state on logout/account change; `useLogout` and `auth-store.clearSession` currently leave `wallet-store` untouched. Verification display is not tied to a user ID.
- `EscrowPanel` returns null unless the project is `InProgress`, hiding escrow completion details when the final approval completes the project.
- The client's funded message says work has started regardless of `workStartedQuery.data`.
- Approval invalidates project/milestone queries but not escrow queries, leaving released totals stale until a later refetch.
- Show explicit wrong-wallet/wrong-network and RPC read-error states, and allow pending deployment status to refresh.

### 7. Define the accepted price and exact amount boundary

Proposals allow an independent `BidAmount`, but acceptance keeps `project.Budget`, and escrow uses that budget. Decide whether bids are informational under fixed-price milestones or whether acceptance must agree a new milestone allocation. Make the resulting payment amount explicit before acceptance.

The backend permits `decimal(18,2)` values, while the frontend converts JavaScript numbers using `amount.toFixed(decimals)`. Two decimal places alone do not make the full permitted numeric range exact in JavaScript. Use decimal strings at the financial boundary or enforce an explicitly safe supported range, and test large values and unsupported token precision. The contract fixes its own funding total, so this gap can cause incorrect allowances or funding failures rather than changing that contract total.

## Final acceptance checklist

- Two distinct users/roles and test wallets: register, login, refresh, logout, reconnect, and switch accounts.
- Create project; allocate all milestones; submit/accept proposal; verify both wallets; create/deploy escrow.
- Reject premature start; approve token allowance; fund and sync; start work using the freelancer wallet.
- Submit, reject/restart/resubmit, and release milestones in order; complete the final milestone and verify both dashboards and balances.
- Exercise payment-success/API-failure recovery, double clicks, reloads, wallet rejection, insufficient balance/allowance, wrong role/account/chain, and unavailable RPC/API.
- Exercise cancellation/refund and repeated sync, including external wallet transactions.
- Extend contract tests for constructor guards, unauthorized release/cancel, invalid/repeated state transitions, and exact-transfer rejection. Add browser coverage for auth/workflow and wallet integration where feasible.

## Documentation and later release work

Update root/frontend/blockchain READMEs, add a frontend `.env.example`, document local chain resets and sample wallets, and replace the hardcoded escrow read-script address. Keep local deployment journals out of future source changes. Review the older installed EF CLI and duplicate frontend lockfile warning. Email verification/reset, notifications, file delivery, AI features, dispute resolution, and production deployment are not established requirements of the documented local MVP; treat them as separate product scope.

## Local restart setup

Docker container: `trustflow-dev-20260915`, loopback port 5433, database `trustflow_dev`. The independent test database is `trustflow_review_tests`. Generated runtime credentials are in ignored `.env.dev-runtime.json`; ignored `.env.review-dev.ps1 api` loads them, applies existing migrations, and starts the HTTP development profile at localhost:5114. Do not commit these local files.

Start Docker Desktop, then the local Hardhat node. On a fresh node deploy MockUSDC first using a new Ignition deployment ID; old journals belong to the previous chain. Start the API and then Next.js with `NEXT_PUBLIC_API_URL=http://localhost:5114/api`. Use `http://localhost:3000` consistently so the frontend origin and refresh-cookie flow match local configuration. A node restart removes its in-memory contracts while the development database retains escrow addresses; use fresh projects for a fresh-chain demo until reset/recovery tooling is implemented.
