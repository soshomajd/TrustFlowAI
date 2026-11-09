# Funded hiring and appendable milestones

Status: implemented and validated in the local API/site, including a live V3 deployment and user-signed 100-token home-page funding and acceptance/start. Canonical funding/start receipts are saved and the stage is InProgress. Delivery/revision, payout and the full four-stage financial walkthrough remain pending; no real-money use or public deployment is claimed.

## Selected user flow

The user requested the full change now, rather than a guided wrapper around the
existing acceptance flow. Old local fixtures are disposable and must not constrain
the design. No old contract is upgraded in place and no wallet is signed by the API.

1. The Client publishes a project and a milestone plan. Publication does not take
   tokens. The plan can include future, explicitly unfunded work.
2. A Freelancer submits a proposal for selected milestones.
3. The Client chooses **Accept and fund**. The server reserves that scope and
   snapshots the participants, exact amounts and terms. The page prepares the
   contract, checks the network/balance/allowance, and asks the Client's wallet for
   the required token approval and funding signatures. Approval is needed only
   when the existing allowance is insufficient.
4. Only a successful, verified on-chain deposit makes the funded offer available
   to the Freelancer. A button click, token allowance, submitted hash or failed
   transaction must never mark an offer funded or accepted.
5. The Freelancer chooses **Accept and start** with their own wallet. Only its
   verified receipt activates the work and finalizes the corresponding accepted
   proposal/assignment. The Client's funding is their consent; there is no extra
   duplicate Client start button.
6. After the active milestone is finished, further work can be added to the same
   Client/Freelancer contract. Adding its financial milestone and receiving its
   exact deposit is one atomic on-chain operation. The Freelancer separately
   accepts/starts the new funded scope. Future plan entries are not funded claims.

Initial funding covers the first selected milestone, not every future plan entry.
For the current cafe example this means 100 test tokens for the home page. The
other three stages remain outside that offer unless the Client selects them.

## Non-negotiable financial rules

- One contract per Client/Freelancer pair on a project; immutable token, Client,
  Freelancer, human arbiter and fee recipient. Neither party may be the arbiter.
- Each deposited milestone is completely funded before acceptance/start. At most
  one unsettled funded milestone is active on a contract in this initial design.
- Stages already paid, cancelled or settled cannot be changed or reused. Append
  uses a new index and a unique funding reference; retries must not add/pay twice.
- Token amounts are integer base units. SafeERC20 and ReentrancyGuard remain.
  Incoming balance delta must equal the declared deposit exactly.
- Cumulative funded gross = cumulative released gross + cumulative client refunds
  + recorded outstanding funds. Unsolicited token transfers are not new deposits.
- Fixed 10% fee is floored once per gross Freelancer award. Net is gross minus
  fee; Client refunds are fee-free. No admin chooses other recipients or a rate.
- Client cancellation is available before the current funded scope is accepted
  and started. Completed payments remain final. Once work is active, preserve
  Freelancer resignation, human-arbiter settlement after 14 days and the 90-day
  stalled-case exit. New deposits cannot restart an existing case's clock.
- An unaccepted funded offer can be cancelled/refunded by the Client's wallet.
  A server timeout or expired reservation never implies that tokens were refunded.
- AI remains advice only. No server, local-admin session or AI signs party funds.

## Contract changes

A new non-upgradeable contract implementation replaces the fixed-total/fixed-array
assumption for newly created contracts. Old local contracts can be disregarded as
fixtures; they cannot physically be rewritten at their existing addresses.

- Keep participant and fee-policy immutability; cumulative deposit total and the
  funded milestone list grow only through authorized exact token deposits.
- An initial funding operation binds the deposit to an offer reference and terms
  hash. Appending funded work binds a fresh reference/hash to the next stage.
- Freelancer acceptance/start is restricted to the current funded reference and
  scope. Release and dispute methods operate only on the active ordered stage.
- Completion of a funded scope leaves a zero-outstanding contract available for a
  later funded append. Explicit cancellation/termination closes it to new deposits.
- Emit independently traceable funding, append, work-start, payout, fee, refund and
  case events with the stage index/reference. Preserve exact transaction and block
  evidence for reconciliation, including after a lost HTTP response or a reload.

## Backend changes

- Add explicit hiring-offer states: preparing, awaiting funding, funded awaiting
  Freelancer, active, declined/cancelled. Keep financial confirmation separate
  from workflow intention and from the old Client-selected proposal state.
- Reserve selected milestones transactionally while an offer is being funded;
  reject overlapping offers. A funded reservation cannot expire back onto the
  marketplace until its actual on-chain disposition is reconciled.
- Snapshot the terms/amounts in offer-milestone records. Map each funded database
  milestone explicitly to its contract index; future planned stages have no
  on-chain index and must never be read as funded by array position.
- Expose prepare/resume/sync/cancel and Freelancer accept/start reconciliation
  operations. All reads/writes remain scoped by role, project and counterparty.
- Replace final acceptance through the old unfunded PATCH path for the new flow;
  a caller must not bypass funding by calling a legacy endpoint directly.
- Verify chain, deployed participants/token/fee policy, exact scope/amount,
  successful receipt and canonical event evidence before promoting financial
  state. Persist hashes and block times; syncing the same evidence is idempotent.
- Validate the public arbiter address differs from both verified party wallets
  before creating a deployment record, with a useful controlled error.
- Use explicit EF migrations. Normal integration tests use isolated PostgreSQL;
  do not migrate/reset the running development fixture until the code is ready.

## Website changes

- A single Client action owns preparation, deployment, wallet approval/funding and
  reconciliation. Show the actual current step, amount and selected milestone.
- Insufficient funds, rejected signatures, network mismatch and reverted receipts
  leave a resumable offer, with no fake success or final accepted badge.
- Persist only non-secret pending operation references; access tokens remain
  memory-only. Resume checks chain state before asking for another signature.
- Freelancer sees funded offers and **Accept and start**; future unfunded planned
  stages cannot start. Both roles see the same assignment and financial evidence.
- Appended stages show their exact additional deposit and require the same proof
  and acceptance sequence. No modification to an already active paid scope.
- Update Client and Freelancer beginner guides in the same implementation.

## Required validation

1. A project can be drafted/published without a debit; posting is not funding.
2. A proposal is initially unfunded. Only the selected scope can be reserved.
3. Low balance/allowance, rejection, revert and wrong chain cannot activate work.
4. Successful funding is visible before Freelancer acceptance; early acceptance
   and nonparty acceptance/release are rejected by contract and API.
5. A lost receipt/API response and concurrent retries cannot deposit, append,
   assign or pay twice. Resume after reload reads canonical evidence first.
6. Client cancellation before start returns all outstanding funds without a fee;
   a simultaneous Freelancer start has one valid winner, never both outcomes.
7. A completed 100-token stage pays 10 fee + 90 net. A 20-gross arbitration award
   pays 2 fee + 18 net and refunds 80; small-unit floor/conservation remain exact.
8. A new 100-token append adds one new stage only if the complete deposit succeeds;
   insufficient funds/repeated reference revert the entire append.
9. New work cannot bypass ordered release, mutate a paid stage, reset an open
   case or change immutable recipients. Arbiter powers and 14/90-day windows hold.
10. Verify both role UIs and the admin financial/timeline records against actual
    local chain receipts, with all live party wallet signatures user-operated.

## Upwork reference and intentional differences

Official Upwork references distinguish a Freelancer proposal from the Client's
formal funded contract offer. Future milestones may be planned before funding.
The selected TrustFlow UX uses familiar labels but preserves self-custody and
explicit wallet signatures. Upwork's review-period auto-release policy is not
being copied into TrustFlow's human-controlled release/arbitration protocol.

- [Hiring from a job post](https://support.upwork.com/hc/en-us/articles/211063618-How-to-send-an-offer-from-a-job-post)
- [Adding/editing milestones](https://support.upwork.com/hc/en-us/articles/360000990268-How-to-add-or-edit-milestones-on-Upwork)
- [Milestone funding and review](https://support.upwork.com/hc/en-us/articles/211068218-How-to-use-milestones-in-fixed-price-jobs)
- [Escrow funding terms](https://www.upwork.com/legal)
