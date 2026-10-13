# TrustFlowAI repository guidance

## Repository map

- `backend/`: .NET 10 ASP.NET Core API, EF Core/PostgreSQL persistence, and integration tests in `TrustFlow.slnx`.
- `frontend/trustflow-web/`: Next.js 16 App Router client. Follow its nested `AGENTS.md` for version-specific Next.js work.
- `blockchain/`: Hardhat 3, Viem, Solidity contracts, tests, and Ignition modules. Follow its nested `AGENTS.md` for contract work.
- `docs/PROJECT_STATE.md`: persistent implementation state, decisions, gaps, milestone, and next step.

## Architecture boundaries

- The frontend consumes the backend REST API; it must not enforce authorization or financial truth on its own.
- The backend owns identity, marketplace workflow, off-chain escrow metadata, and chain reconciliation; it must not custody or sign for user funds.
- The blockchain contract is the authority for escrow funds. Each project gets one non-upgradeable `TrustFlowEscrow` instance with immutable participants/token/total and fixed milestone amounts.
- Keep subsystem dependencies and builds independent; do not add cross-subsystem generated files as shared source.

## Naming and style

- C#: `TrustFlow.Api.*` namespaces, PascalCase types, async controller actions with cancellation tokens, feature-grouped DTOs. Preserve the existing `MileStone`/`MileStones` spelling unless a migration is intentional.
- Frontend: kebab-case files, PascalCase components, `use-*` hooks, and feature slices under `src/features/<domain>/{api,components,hooks,queries,schemas,types}`; use `@/` imports.
- Solidity contracts and Ignition modules use PascalCase. For TypeScript, follow the subsystem config and surrounding formatting; no repository-wide formatter is configured.
- Preserve public API shapes, persisted enum strings, and migrations unless the task explicitly changes them.

## Standard commands

- Backend (repo root): `dotnet restore backend/TrustFlow.slnx`; `dotnet build backend/TrustFlow.slnx --no-restore`; `dotnet run --project backend/TrustFlow.Api --launch-profile https`. Runtime requires `ConnectionStrings__DefaultConnection` and a 32+ byte `Jwt__Key`, plus `Blockchain__ArbiterAddress` (the reviewer's wallet, never a server key), `Email__Host` and `Email__FromAddress` (and `Email__User`/`Email__Password` if the server needs a login) outside Development; in Development an unset mail server logs each email, code included, to the API log. The AI assistant defaults to `Ai__Provider=Mock` (free, labelled demo answers); `Ai__Provider=Anthropic` needs `Ai__ApiKey` from user secrets or an environment variable, never from a settings file. `Review__AdminEmails__0` (and further indexes) names who gets the reviewer role. Apply EF migrations explicitly outside `Testing`.
- Backend integration tests require an isolated PostgreSQL database: set `TRUSTFLOW_TEST_DB`, then run `dotnet test backend/TrustFlow.Api.IntegrationTests/TrustFlow.Api.IntegrationTests.csproj`.
- Frontend (from `frontend/trustflow-web`, with valid `NEXT_PUBLIC_API_URL`; set `NEXT_PUBLIC_SITE_URL` to the public address for any build that will be deployed, because canonical links, the sitemap and link previews use it): `npm ci`, `npm run dev`, `npm run lint`, `npx tsc --noEmit --incremental false`, `npm run build`.
- Blockchain (from `blockchain`): `npm ci`, `npx hardhat compile`, `npx hardhat test nodejs` (or `npx hardhat test`), `npx hardhat node`; local deploy/read: `npx hardhat ignition deploy ignition/modules/TrustFlowLocalModule.ts --network localhost`, then `node scripts/readLocalEscrow.mjs`.

## Validation rules

- Run the smallest validation that covers the change; broaden only at an integration boundary or when multiple subsystems can be affected.
- Do not rerun expensive unchanged builds/tests merely for reassurance. Record material validation results in `docs/PROJECT_STATE.md` when they prevent repeated work.
- Never use a shared or production database for integration tests; the test host migrates `TRUSTFLOW_TEST_DB` and leaves accumulated data.
- Do not inspect generated/dependency output (`node_modules`, `.next`, `bin`, `obj`, `artifacts`, `cache`, `coverage`, or Ignition deployment journals) unless the task requires it.

## Security rules

- Keep private keys, JWT keys, database credentials, and environment files out of git. Runtime secrets come from environment variables/user secrets/Hardhat keystore.
- Keep browser access tokens memory-only; retain the credentialed refresh-cookie flow rather than persisting tokens in browser storage.
- No backend, admin, or AI custody of funds or final financial decisions. Client/freelancer wallet actions remain user-signed. The one exception is the escrow's human arbiter: a single address fixed into each contract at deployment, whose key lives in the reviewer's own wallet or a multisig and never on this server. It can act only on an open case, only after 14 days, and only by splitting that milestone between the client and the freelancer (optionally ending the escrow); it can never send funds anywhere else.
- AI output is advice. It never signs, releases, refunds or decides where funds go, and nothing it writes is executed. Every call goes through `IAiGateway`; user text reaches the model only through `AiPrompt.Fenced`; people appear to it only by role. A dispute ruling and the advice shown to the reviewer are recorded opinions; the reviewer reads them and decides.
- Preserve one escrow per freelancer per project, complete-budget funding, ordered one-time milestone release, client-controlled releases, freelancer-controlled work start, and client cancellation only before work starts. After work starts the only other exits are the freelancer resigning, a case settled by the arbiter after the 14 day window, and the 50/50 split anyone can force after 90 days of arbiter silence.
- Chat between a client and a freelancer is plain text tied to one project. The backend refuses (422) any message holding a phone number, email address, link or messenger handle, so the deal and the payment stay inside the escrow; do not weaken or bypass `ContactInfoDetector`, and do not add attachments to chat (files belong in the delivery flow).
- On-chain token amounts are integer base units. MockUSDC has 6 decimals and is local/test-only; conversions must be explicit and exact.
- Preserve `SafeERC20`, `ReentrancyGuard`, wallet ownership verification, role authorization, and the backend escrow's `PendingDeployment` state.

## Project-state workflow

- Before every new task, read this file and `docs/PROJECT_STATE.md`, then inspect only task-relevant files and any applicable nested `AGENTS.md`.
- Patch `docs/PROJECT_STATE.md` only when implementation state, the active milestone, the next step, a known issue, or an important decision changes; do not rescan or rewrite it wholesale.

## User guidance

- Keep the Client and Freelancer `How to get started` pages current. When a user-facing feature changes either role's path, update the matching guide in the same change; update both guides when the feature affects both roles.
- Guide copy must use short, simple English for beginners and cover only actions that are available in the product.
