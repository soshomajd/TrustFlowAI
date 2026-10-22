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

## Admin overview and access

For temporary local inspection, an explicitly enabled Development-only preview
can open the existing admin panel without password/email-code entry. Set
`AdminSecurity:LocalPreviewEnabled=true` and `AdminSecurity:LocalPreviewEmail`
in backend user-secrets, and `TRUSTFLOW_LOCAL_ADMIN_PREVIEW=1` in ignored
`.env.local`. It requires an existing allowlisted Admin account and loopback
access. Tokens are memory-only, expire within 30 minutes, have no refresh cookie
and allow reads only. Account passwords, email verification and MFA are preserved.
Disable the backend flag and remove/set the frontend flag to `0`, then restart
both services to restore normal local entry. Neither flag enables this preview
in production. No wallet is required for the overview or project list.

Admins sign in at `/admin/login`; `/dashboard/admin` and the Review desk route
anonymous visitors there. Old `/login?next=/dashboard/admin...` links also lead
to the dedicated page. Its API endpoint `/api/auth/admin/login` checks a valid
password, the Admin role and email confirmation before emailing a one-time code;
`/api/auth/admin/mfa` issues the session only after that code is verified. A
participant password cannot obtain a session from that endpoint. Sign-in is
rate-limited and keeps Identity lockout, the memory-only access token and the
existing HttpOnly rotating refresh cookie. Successful admin sign-ins are audited.

The platform owner approves emails through trusted `Review:AdminEmails`
configuration (`Review__AdminEmails__0`, then further indexes). Existing selected
accounts receive the role at startup. New administrators use `/admin/setup`,
which calls `/api/auth/admin/setup`: unapproved addresses are refused, the server
assigns only Admin, and email confirmation is required. The user chooses their
own password. There is no Client/Freelancer picker or public signup control on
the admin entry page. Public participant registration redirects approved admin
addresses to this activation flow instead of creating a participant identity.
Existing accounts use sign-in or password recovery; activation cannot replace an
account or password. Admin verification and recovery remain under `/admin/...`.
Participant Development email logs are still used when SMTP is unset; admin
activation/sign-in refuse log-only delivery. Real administrator email acceptance
currently remains blocked by the local macOS SMTP certificate-revocation failure.
The explicit local read-only preview does not mark the account email confirmed.

The overview shows project status counts, escrow records and open cases from
`GET /api/admin/overview`. Each successful read writes an `AdminAccessLogs` entry
for the authenticated admin; responses are private and non-cacheable. Apply the
additive `AddAdminAccessLogs` migration explicitly before running the new API.
Clients and freelancers cannot read these platform totals. The existing Review
desk remains available under its existing permissions.

Admins can also open `/dashboard/admin/projects`. The server-side list supports
literal, case-insensitive search by title or participant name, exact project ID,
status, client/freelancer ID, and UTC creation dates. It includes all saved project
states, current assignees and historical escrow parties. Results show exact-text
budgets and stage/escrow/proposal/open-case counts, ordered newest first with ID
as a stable tie-breaker. Pages contain at most 100 projects. API `from` is
inclusive and `to` exclusive; the UI includes the entire selected Through day.
Each successful list request records one access entry plus the IDs of displayed
projects. Invalid or unauthorized requests expose no rows or audit details.
Project titles now open `/dashboard/admin/projects/[projectId]`. The read-only
dossier includes project metadata, exact budget and paginated participants,
milestones, proposals and escrow contracts. Past escrow parties and historical
proposal authors remain visible. Current account wallets are distinguished from
wallets fixed into each saved contract. Data comes from saved records; opening
the page does not initiate blockchain reconciliation or request a wallet signature.
Detail/section reads and displayed resource IDs are audited. The Deliveries
section now shows every saved submission version, its original author, notes,
client change requests and attached-file metadata, including historical authors
after reassignment. File downloads use an authenticated, project/submission/file
scoped admin route and are audited separately. Files are downloaded as attachments;
external delivery links are opened manually and are never embedded. Viewing a
delivery cannot approve it, request changes or move funds. The Financial history
section pages saved escrow summaries and per-escrow milestone payouts/settled cases.
Exact strings preserve gross awards, recorded fees, net freelancer receipts and
known refunds. Each contract retains its network/token identity; case outcomes are
not added to the totals again. Funding/cancellation references are shown when saved.
Missing payout hashes/timestamps, incomplete legacy payouts and untraced termination
refunds are explicitly unknown. These are saved accounting records, not live balances.
Finance reads and returned resource IDs are audited under the authenticated admin.
The selected escrow's CSV report includes its whole saved summary, payouts and
settled cases, independently of UI pagination. Export uses one consistent database
snapshot and records the administrator plus exported resources before returning
an attachment with private/no-store, nosniff and sandbox headers. Reports exceeding
10,000 payouts or cases are refused rather than silently truncated. CSV text is
quoted and formula-like values receive a protective apostrophe. Import the value
column as Text in spreadsheet apps to retain exact decimal strings/long IDs.
The privacy note on the page and in the report explains access logging and careful
sharing/storage; no report is emailed or uploaded to an external service.
The Conversations section lists saved threads separately for each freelancer, then
loads messages only after explicit selection. Admin-only cursor pages preserve
historical threads and participant unread markers; each viewed thread/message ID
is audited. Text is escaped with no active links, attachments or reply/edit/delete
controls. Older/Newer pages use a pinned upper message boundary; Refresh latest
messages starts a new view. The Disputes section separately pages saved escrow
cases and written disagreements, then loads scoped details on selection. It shows
recorded deadlines/contract identity/reviewer notes, exact settlement amounts,
original written parties and saved AI opinions as advice only. Historical provider
labels and missing/corrupt opinion text remain explicit. No chain sync, new AI call,
settlement or note editing occurs here; the existing reviewer case can be opened
separately. All successful list/detail reads are audited. New administrative
mutations remain separate work.

Run `node --test tests/admin-overview.test.mjs` for the admin landing-page states,
role-specific navigation and default dashboard routing. Backend
`AdminOverviewTests` cover role authorization, aggregate-only responses and
durable audit writes against the isolated test database. Run
`node --test tests/admin-projects.test.mjs` for list rendering, role navigation
and filter conversion. `AdminProjectsTests` cover server-side search, filters,
pagination, historical parties, exact budgets and displayed-row auditing.
Run `node --test tests/admin-auth.test.mjs` for admin entry/activation copy,
role-free setup validation, guarded content and safe return destinations.
`AdminAuthTests` cover approved-only setup, role isolation, verification, lockout,
refresh cookies and rejection of participant credentials at admin sign-in.
Run `node --test tests/admin-dossier.test.mjs` for dossier rendering, exact money,
historical states, escaping, empty/stale/error recovery and read-only controls.
`AdminProjectDossierTests` cover Admin authorization, project isolation, bounded
paging, historic parties, exact amounts, durable auditing and unchanged workflow.
`AdminDeliveryTests` additionally cover version history, historical authors,
attachment metadata privacy, scoped file downloads, headers/bytes and audit writes.

Run `node --test tests/admin-finance.test.mjs` for exact financial display,
unknown-versus-zero amounts, network separation and failure recovery.
`AdminFinanceTests` cover authorization, exact accounting, refunds, bounded paging,
project/escrow isolation, legacy gaps and read-only actor/resource auditing.

Run `node --test tests/admin-conversations.test.mjs` for admin thread metadata,
plain-text history, privacy and error states. `AdminConversationTests` cover
420-message cursor paging, historical access, project/thread isolation, read-marker
preservation and actor/resource auditing; participant `ChatTests` remain unchanged.

Run `node --test tests/admin-disputes.test.mjs` for separate record types,
read-only details, exact outcomes, AI labels and partial/error states.
`AdminDisputeTests` cover role/project isolation, recorded deadlines, historical
parties, corrupt/partial opinion records, audit attribution and unchanged financial/AI state.

## Verification

```bash
npm run lint
npx tsc --noEmit --incremental false
npm run build
```

With Node.js 24 or newer, run `npm run test:amounts` for exact conversion, large-number and real form-schema checks, `npm run test:fees` for per-payment 10% fee rounding/conservation, and `npm run test:escrow` for funding/refund, work-start and milestone-payment recovery tests. These inject API and receipt-read failures without signing transactions. Submitted hashes are retained for retries within the same page. After reloading a page, wait for any pending wallet transaction to finish and use **Refresh on-chain status** before trying again. This read-only action is available while awaiting funding and after funding. After a confirmed payment with a failed approval response, reopen the same milestone approval and use Finish approval when shown; retries check the existing release before requesting a signature, and the API returns an already-recorded approval without crediting it again.

The Client and Freelancer **How to get started** pages describe the current product flow. New escrows take a fixed 10% fee from gross freelancer awards; refunds remain fee-free, and V1 escrows retain their original terms. Past escrows remain visible after cancellation or resignation. Chat is plain text, project-scoped, and has a Load older messages action. Contact information is rejected by the API; files use the delivery flow.

A Hardhat restart removes on-chain deployments but leaves database history. Use a fresh project after restoring the test token; do not assume an old database address still contains a contract. Public deployment is currently deferred. See [project state](../../docs/PROJECT_STATE.md) for completed checks and remaining acceptance work.

Amounts are parsed without rounding, with 0–18 token decimals supported. Funding checks read the contract total directly. Numeric API request shapes are preserved; the forms refuse input whose cents would change during JSON serialization. Reviewer signing preserves the entered decimal text and checks it against the exact contract amount. Full exact-string monetary API/display support remains separate work for legacy values that cannot be represented by JavaScript numbers.


Monetary read responses include decimal-text companions such as `budgetExact`,
`amountExact`, `platformFeesPaidExact` and `freelancerNetPaidExact`. Displays and
read-side sums use these strings through `src/lib/money/exact-money.ts`; do not
convert them to JavaScript numbers. Existing numeric API fields and request
bodies remain compatible. Project/stage forms still refuse values that the
numeric request format cannot send exactly. Run `npm run test:amounts` for
conversion, request-boundary and exact-display regressions.
