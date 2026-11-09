# Local acceptance checkpoint — 26 October 2026

**Superseded fixture:** later on 26 October the user explicitly requested deletion of all disposable local application data and creation of fresh Client/Freelancer/Admin accounts. All previous application rows, including the fixture, old identities and wallet proofs described below, were removed from `trustflow_dev`; on-chain contracts were left untouched. Three fresh role-scoped accounts were provisioned and sign-in/local-admin access verified. Start a new fixture and reverify wallet ownership. The chronological acceptance evidence and unresolved source-audit findings below are retained as history, not current database state. See the latest reset entry in `PROJECT_STATE.md`.

**New fixture after reset:** the fresh Client has verified its wallet. At the user's request, created **LOCAL QA - Cafe website - fresh four-stage workflow** (`d8fd7bce-ea77-4729-beb5-b42f9f77f1c7`) through the normal API. Its budget is 400 local test tokens, exactly allocated to four Pending/unassigned 100-token stages with descriptions/deadlines. The paths are revision/approval, standard payment, 20% partial arbitration on the third stage, and continued work/final completion. It is Open with zero active proposals and ready for the user's review. No fresh escrow, funding or financial walkthrough exists; API preparation is not native-Brave form acceptance. The old Sara/Reza records below remain historical.

**Latest checkpoint, 27 October:** the user reviewed the project in Brave, then requested a proposal for **only the home-page stage**. Submitted the 100-token/3-day proposal through the normal Freelancer API and verified Client visibility. The user saw it Pending and rejected it. An independent API read confirms Rejected, zero active proposals, the stage still Pending/unassigned and `canSubmit=true` for this freelancer. The user postponed the requested resubmission before it happened: **no second proposal was sent**. Resume from home-page-only reapplication; the original full-project financial scenario is not the currently accepted proposal scope. No new escrow or financial transaction exists.

**Resumed checkpoint, 28 October:** the user explicitly requested resubmission. A different **second** home-page-only proposal is now **Pending**, gross 100 test tokens and estimated 3 days. Client-side API verification confirms it is visible, covers only the first milestone and is the sole active proposal; the first proposal remains Rejected. All stages are still Pending/unassigned. Next is the user's Client review/acceptance; no new escrow, wallet prompt or payment exists. Submission used the normal API, not an automated native-Brave form.

**Subsequent acceptance:** the user accepted that second proposal. Verified API state is Accepted/InProgress with only the first 100-token stage assigned; all stages remain Pending/unpaid and the other three remain unassigned. GET escrow is 404 and the participant's escrow is null. The Client wallet is verified but the fresh Freelancer wallet is not. Next is user-operated Freelancer wallet ownership verification, then setup of the 100-token escrow. No deployment, funding, work start or financial signature was performed.

**Latest wallet check:** the fresh Freelancer subsequently verified the **configured arbiter/fee-recipient address**, confirmed by the authenticated wallet API and the green native-Brave badge. The Client remains separately verified. The constructor rejects an arbiter who is also a participant, so escrow creation/deployment is blocked until the user chooses a distinct Freelancer wallet and an authorized local recovery plus fresh ownership proof corrects this binding. Normal verified-wallet replacement is intentionally unavailable. No escrow was created and no financial request was sent.

**Authorized local wallet recovery completed:** at the user's explicit request, cleared only the fresh Freelancer's six wallet-binding/challenge fields after checking the exact account/role and absence of any escrow. The API now returns no wallet/`isVerified=false`; Client verification, the first Rejected proposal, second Accepted proposal and home-page assignment are preserved. No password, other account, configuration or on-chain state changed. Next is user-operated selection of a separate Freelancer wallet and fresh ownership proof through the normal UI. Refresh the page to discard the stale green badge; no site sign-out is required.

**Replacement confirmed; financial-flow discussion:** the user verified the separate intended Freelancer wallet. The API confirms both participant wallets are verified/distinct from the configured arbiter and the sole 100-token assignment still has null escrow. No new creation/deployment/funding action was run. The user is reviewing a different product model (funding at project creation/publication, later milestone additions to the same contract and mutual start confirmations) and requested official Upwork research before continuing. This is a pending design decision requiring a new contract version if selected; no protocol or UI implementation has changed.

This is a resumption record, not a claim that the live financial walkthrough is complete. The user requested a full client/freelancer/arbiter workflow in separate native Brave profiles, followed by a project gap review. Wallet confirmations remain user-operated. Public deployment and email troubleshooting remain deferred.

## Latest administrator-access decision

The user subsequently explicitly approved password/email-free local administrator operations. Direct entry is **http://localhost:3000/admin/local**; open it in the chosen Brave profile to obtain a new session without manual sign-out. This supersedes the read-only-only requirement and the normal-sign-in blocker described in the earlier chronological notes below. Frontend automatic local entry remains enabled and backend `AdminSecurity:LocalReviewEnabled=true` is set in user-secrets.

The new capability is Development/loopback/local-RPC/chain-31337-only, bound to the configured allowlisted Admin account and current security stamp/role. All existing admin reads/downloads/exports and local case note/advice/sync operations are available; wallet verification still requires its existing signed challenge. Other protected writes are refused. Human wallet settlement, immutable arbiter/recipients, contract windows and fees are unchanged. Normal online password/email/MFA is untouched. Old preview sessions remain read-only until the direct entry is opened.

Validation now totals **381 backend / 218 frontend tests passing**, plus TypeScript, lint and final build. Live API checks confirm `readOnly=false`/`localReview=true`, authorized admin/reviewer reads 200, anonymous admin 401 and direct frontend entry 200. Native Brave confirmation of the refreshed capability and live arbitration remain pending. No wallet transaction was submitted.

Subsequent native Brave captures now confirm the new decisions-enabled notice in Review desk and the project dossier. User-operated wallet verification is complete: the account's verified wallet is exactly the saved arbiter address, confirmed read-only in PostgreSQL and by the green UI badge. Review desk correctly has no open cases. The dossier shows the fresh project's 400 budget, four milestones, one proposal, zero escrows and zero deliveries; its proposal is still Rejected in the database. The next step is reapplication by Reza and acceptance by Sara, followed by client-side escrow creation/funding. The admin dossier inspects those records; it does not create a participant escrow.

## Verified saved fixture

- Project: **LOCAL QA full workflow - Sara and Reza - Oct 23**.
- Project ID: `429f9c14-166c-4f56-8393-d672afe9fecf`.
- Sara is the client; Reza is the intended freelancer.
- Budget: **400 test tokens**, four milestones of **100** each.
- On 26 October, a read-only query of loopback `trustflow_dev` confirmed the project is **Open**, all four milestones are **Pending/unassigned**, the initial 400-unit proposal is **Rejected**, and **no escrow exists for this project**.
- No new native-browser walkthrough has been verified in this resumed session. The saved database state supersedes the earlier interrupted browser-dialog checkpoint.

| Milestone | Intended acceptance path | Current saved state |
| --- | --- | --- |
| Revision and corrected delivery | Submit v1, request changes, restart, submit v2, approve | Pending/unassigned |
| Standard delivery and payment | Ordinary delivery, approval and payment | Pending/unassigned |
| Partial arbitration - 20 percent | Open a financial case, award 20% of this stage, continue escrow | Pending/unassigned |
| Continue after arbitration and finish | Start after settlement, deliver and approve | Pending/unassigned |

## Read-only local chain baseline

Rechecked on 26 October: chain **31337**, block **16**, MockUSDC **6 decimals**. No chain write or time advance was performed in this resumption.

| Account/resource | Token balance |
| --- | ---: |
| Sara | 0 |
| Reza | 90 |
| Platform fee recipient / arbiter | 10 |
| Earlier funded retry escrow | 100 |

The earlier retry escrow still reports `workStarted=false` and is unrelated to the fresh four-stage fixture. Preserve it.

## Completed code corrections in the current worktree

- Preserve the platform fee when settlement is discovered after the backend missed the open case.
- Validate ending-settlement refunds against the disputed stage plus later unpaid stages, including prior partial refunds, rather than rejecting a legitimate ending event.
- Anchor execution-event times and settlement eligibility to the canonical chain clock. Local 14-day simulation must not depend on the Mac/browser clock.
- Check receipt success and actual resulting contract state for payment/refund requests, withdrawal, resignation and the 90-day fallback. Same-page retries retain the submitted hash and do not request another signature after receipt/API interruption.
- Guard submission and change-request writes atomically so stale HTTP requests cannot reopen a paid/settled/reassigned stage.
- Refresh milestone/proposal lists while open and provide explicit refresh controls. Both beginner guides explain refresh and case-clock checks.

Validation on a fresh isolated `trustflow_acceptance_tests_20261026`: **372/372 backend tests pass**. **216/216 frontend tests pass**; after a test-harness-only lint correction, the six readiness tests and full lint pass. Nonincremental TypeScript, production website build and whitespace checks pass. Solidity source was unchanged and its suite was not rerun.

The local API was restarted with these compiled fixes. OpenAPI and the homepage return **200**; anonymous admin overview returns **401**. This proves service readiness and anonymous rejection, not signed-in browser acceptance. The current changes are **not committed or pushed**.

## Next live steps

1. Establish an actual authenticated administrator session and the immutable arbiter wallet in a separate profile. The existing local admin preview is GET-only and cannot save/sign decisions; it must not be presented as an authenticated financial reviewer session.
   - The user chose to reuse the freelancer profile temporarily, signing out of Reza without leaving the project; Sara's client profile stays separate. Native capture shows soheil's admin overview and a connected, not-yet-verified wallet. MetaMask's displayed account prefix/suffix agree with the stored arbiter, but its full address/network/ownership still need confirmation.
   - Automatic frontend preview entry is now disabled locally (`TRUSTFLOW_LOCAL_ADMIN_PREVIEW=0`). The existing `/admin/login` password/email-code form returns 200 and is present in the rendered page. The user must sign out of the current preview session before using that form; the wallet can stay connected. No backend preview gate, account credential, confirmation flag or MFA policy was changed. No code was requested by the agent, and actual email delivery remains unverified.
   - Later in the same session the user reported having no administrator password. A single normal password-recovery request returned generic 202 but recorded a fresh SMTP TLS/certificate failure; email delivery did not succeed. The frontend preview flag was restored to `1` and its entry verified, preserving read-only local access. This supersedes the immediately preceding flag state. No password, confirmation/MFA flag or preview write permission was changed, and no one-time login bypass was created. Resolve the earlier email-troubleshooting deferral with the user before attempting that separate repair.
2. Reza reapplies for all four stages; Sara accepts the new proposal. Verify the rejected proposal remains in history.
3. Create/deploy the fresh escrow and inspect its immutable participants, 400-unit total, token, milestone order, 10% policy and fee destination.
4. Sara obtains local test tokens and signs token permission/funding. Do not request a signature until the actual wallet request is visible and its role/amount are verified.
5. Execute the four paths above using the separate role profiles. Record each confirmed receipt and the actual before/after token balances.
6. Verify common case reference/reason, independent result-notice acknowledgements, timeline/finance receipts and continued work after partial settlement.

Expected deltas **only if that scenario completes as planned**: Reza **+288**, fee recipient **+32**, Sara receives **80 back** from the 400 deposit, new escrow balance **0**. The third stage awards 20 gross / 2 fee / 18 net; this is not 20% of the whole project. These are expectations, not observed results.

Mutually exclusive exits (pre-start cancellation, resignation, ending arbitration and 90-day fallback) require separate escrows; one completed contract cannot establish all branches. Any accelerated waiting-window test must be explicitly local-only and labelled simulated.

## Remaining audit findings to resolve or scope

These are source-review findings, not a declaration that deployment acceptance is complete:

- `EscrowFundingSyncOrchestrator.ApplySettlementAsync` still invents the missed case's kind from the payout and uses reconciliation time as its opening time. Recover `CaseOpened` evidence instead; a payment amount does not identify who opened the case.
- The terminal Completed branch can process an escrow still saved as AwaitingFunding, while `ApplyCompletionAsync` claims only Funded rows. Add a missed-funding/full-offline-lifecycle regression before changing that transition.
- `EscrowExecutionTrace.CaptureAsync` returns early once expected evidence rows exist. Previously stored block evidence is not revalidated on that path after a reorg/local chain reset.
- `Program.cs` runs rate limiting before authentication even though the AI/chat policies request an authenticated user ID. Verify the middleware ordering and per-user partition behavior outside the Testing environment, where rate limiting is currently disabled.
- `ReviewerAccess.SeedAsync` grants roles from configuration but does not revoke a stored Admin role when an address is removed. Decide and test whether the list is authoritative access control or only role bootstrap; do not silently revoke roles while changing unrelated workflow code.
- The contract permits an opener to withdraw/reopen, restarting the case clock. Review the resulting liveness/fairness behavior before public release. Any protocol change needs a deliberate new contract version and specification; existing funded immutable escrows cannot be patched.
- Real administrator activation/MFA acceptance, cross-page pending transaction tracking, report/moderation queue, staff permissions and deployment security/operations acceptance remain separate work. The user has deferred the known Gmail TLS issue and public deployment.


## 2026-10-28 funded-hiring replacement checkpoint

The same cafe fixture now uses V3 and its second proposal is reserved/awaiting funding. Its new contract has been deployed and verified live, but holds zero tokens and has not started. Only the home-page stage (100 local test tokens) is reserved; three stages remain unassigned. The user has the exact Client workspace link. Client test-token balance is zero and gas is available: next is the user-operated Get test funds action, then funding and Freelancer acceptance/start. No new project/proposal is needed. Client/Freelancer views were checked in isolated Brave sessions, with a Client mobile layout check; actual MetaMask signatures and the live four-stage financial walkthrough remain pending.


### User-confirmed home-page deposit

The user confirmed the actual local wallet funding. Independent chain reads show 100 mUSDC in the V3 escrow, total deposits 100, Funded state and no work start. The normal API already reports Funded and contains the stage transaction hash/block time; no reconciliation repair or second transfer was required. Next is the Freelancer's Accept and start signature for this same funded home-page scope.

### User-confirmed home-page work start

The user subsequently signed **Accept and start** with the Freelancer wallet. Independent chain/API reads confirm workStarted=true, the home-page milestone InProgress, proposal Accepted, and the canonical start receipt already saved. Deposits remain 100 local mUSDC and releases remain zero; the other three milestones are Pending. No manual reconciliation or further transaction was needed. Next is the test delivery, Client revision request and Freelancer resubmission, then the Client-signed release with exact fee/net balance verification. The complete four-stage scenario is not yet finished.

### 2026-10-29 first home-page delivery

The user submitted version 1. An authenticated Client API read confirms Submitted status, one PNG attachment and a preview link; the saved note is `delivered`, Client feedback is absent and PaidAmount is null. This check did not download/review the image or visit the external link, change the submission, request revision or authorize a payout. Continue with the Client revision request, Freelancer resubmission and then user-signed approval/release.

The user subsequently requested changes from the Client account. An authenticated Freelancer API read confirms Rejected status, feedback and ReviewedAt saved against the single version-1 delivery, and PaidAmount still null. No second delivery exists yet. Next is Freelancer **Restart milestone** followed by a revised submission; funding and on-chain acceptance/start must not be repeated.

The user then submitted version 2 as another disposable test delivery. An authenticated Client read confirms Submitted status and two saved versions: the new version has a preview link and no feedback/review yet, while version 1 retains its original file/link and Client feedback/review time. PaidAmount remains null. Next is Client approval/release signed by the user, then independent receipt and exact 90/10 balance checks. No financial signature or transaction was sent by the agent.

### Home-page approval and release verified

The user approved version 2 and signed the release. Independent local chain and Client API reads agree: Approved milestone, 100 gross released, 10 platform fee, 90 Freelancer net, Completed escrow and zero contract token balance. Native Brave confirms the approved stage and amounts; the other three stages remain Pending/unfunded and can reuse this Freelancer contract. No agent-signed transfer occurred. The user then raised original-file copying before payment and requested official Upwork research; no preview/original gate or automatic release policy has been implemented. Resolve that product concern before resuming the remaining stages.

### 2026-10-31 resumed protected-delivery checkpoint

The subsequently approved preview/original separation is now implemented. Client original/source access requires the full gross milestone award and its exact saved chain evidence; partial/refund outcomes remain locked. Image previews are reduced/watermarked, PDF previews contain only bounded first-page text/cards, and original author/admin/arbiter access and all version history remain available. The saved closing checks pass: 89 affected backend tests, 237 frontend tests, TypeScript, affected lint and final production build.

Signed-in Brave preview/mobile acceptance is still unverified: the first attempt timed out at login, and on resumption the private temporary QA credentials were absent. Both services are up; no passwords/accounts were reset and no user profile or financial state was changed. Fresh read-only development DB and chain checks retain the home-page Approved result (100 gross, 10 fee, 90 net, zero escrow balance) and the three later Pending/unfunded stages. Next is authorized browser acceptance, then the next menu-stage funding/acceptance in the same contract. Current changes remain uncommitted/unpushed.

### 2026-10-31 Brave acceptance completed with a separate unpaid fixture

The existing signed-in Client Brave session was available. Refresh deliveries exposed Download original on the fully paid home-page delivery; the original response reached the native Save dialog (cancelled), while both versions and prior feedback remained visible. No account reset was required.

The user then authorized creating an unpaid test scenario. A separate integration database, existing fake chain/scanner, normal disposable QA Client login and temporary website copy served this fixture on loopback ports 5115/3001, separate from the real localhost session. Its genuine API policy rejects the unpaid original with 403, hides the source link, and returns a reduced 960 x 686 PNG preview. Native Brave confirms only Review preview is available, its repeated watermark image loads on desktop and a 390 x 844 mobile viewport, and an offline error recovers through Retry after network restoration. Network/device emulation was restored. Private screenshots are in `/private/tmp/trustflow-isolated-delivery-qa`; credentials and temporary helpers are not repository files.

File-protection browser acceptance is complete. This fixture's synthetic chain is not a live financial test, and no wallet transaction was submitted. The real cafe scenario is still paid only through stage 1; next is the 100-token menu stage in the same Freelancer contract, then percentage arbitration and final handover. Current changes remain uncommitted/unpushed.
