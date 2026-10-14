# TrustFlow Escrow Smart Contract Specification

## Purpose

TrustFlowEscrow is an ERC20 escrow contract created for one TrustFlow project.

The Client deposits the complete project budget.

The Freelancer receives payments milestone by milestone after Client approval.

One contract belongs to one project only.

## Actors

### Client

The Client can:

- Fund the escrow
- Release an approved milestone payment
- Cancel the escrow before work starts
- Receive a refund when cancellation is allowed

### Freelancer

The Freelancer can:

- Mark project work as started
- Receive released milestone payments

### TrustFlow Backend

The backend can:

- Deploy the contract
- Save the contract address
- Read contract state
- Read blockchain events
- Synchronize blockchain state with the database

The backend cannot directly move escrow funds unless it owns the Client wallet.

## Constructor Inputs

The contract receives:

- ERC20 token address
- Client wallet address
- Freelancer wallet address
- Ordered milestone amount array

Example:

```text
tokenAddress
clientWalletAddress
freelancerWalletAddress
[3000, 4000, 3000]
```

## Cases, the arbiter and ending early

Added after the first version. The client still releases payment, and nothing below blocks that.

### Arbiter

The constructor takes a fourth address, the arbiter (`token, client, freelancer, arbiter, amounts`). It is immutable, must not be zero and must differ from the client and the freelancer. The arbiter can do exactly one thing: settle an open case.

### Cases

Only the milestone that is next in line can have a case, and only one case is open at a time. A case never blocks anyone.

- `requestPayment()` (freelancer): says the milestone is delivered and asks to be paid.
- `requestRefund()` (client): says it was not delivered, or not good enough, and asks for it back.
- `withdrawRequest()`: only the side that opened it.
- `releaseMilestone(i)` by the client closes any open case.
- Both need the escrow funded and work started.

### Settling

`settleCase(index, freelancerAmount, endEscrow)`, arbiter only, after `RESPONSE_WINDOW` (14 days) from the moment the case opened. The index must be the next milestone; `freelancerAmount` is at most the milestone amount, the rest of that milestone goes to the client. With `endEscrow` everything unreleased after it also goes to the client and the state becomes `Terminated`; settling the last milestone completes the escrow instead. `getMilestonePaid(i)` returns what the freelancer received, and `isSettledByCase(i)` tells a settlement from a normal release.

### Leaving

- `resign()` (freelancer, after work started): everything unreleased goes to the client, state `Terminated`. Milestones already paid stay paid.
- `settleStalledCase()` (client or freelancer): if the arbiter has not acted `STALL_WINDOW` (90 days) after a case opened, the milestone is split in half and the escrow ends, so funds cannot stay locked for ever.

### Accounting

`releasedAmount` is what the freelancer received, `refundedAmount` what went back to the client, `remainingAmount() = totalAmount - releasedAmount - refundedAmount`. `EscrowState` gained `Terminated` (value 4).

### What the arbiter can and cannot do

It cannot act without an open case, before the window, on any milestone but the next, give the freelancer more than the milestone, or send funds to any address other than the client and the freelancer. A stolen arbiter key can therefore only misdirect one milestone (or, with `endEscrow`, the unreleased remainder) between the two parties of a case that one of them opened; use a multisig for it.


## V2 platform fee (new deployments)

`TrustFlowEscrowV2` retains the V1 workflow and participant permissions and adds a fixed receiving wallet and a fixed 1000 basis point fee. Existing V1 instances are non-upgradeable and remain fee-free. New V2 deployments require an explicitly supplied, nonzero fee recipient; no backend signing key is required for it.

Every ordinary milestone release or case settlement first determines the gross freelancer award. The contract computes `fee = gross / 10` with integer floor rounding once per payout, transfers `gross - fee` to the freelancer and `fee` to the immutable receiving wallet. The client's refund is calculated from the gross award, never from the net payment. The 90-day fallback charges only the freelancer's half. Pre-start cancellation, a full client refund and resignation of unpaid work charge no fee. Past payments are never charged again.

`releasedAmount`, `getMilestonePaid`, `MilestoneReleased` and `CaseSettled` retain their gross semantics for V1 compatibility. `getMilestoneFee`, `platformFeesPaid`, `freelancerNetPaid` and the `FreelancerPaid` event expose the fee and net transfer. Conservation is `gross awards + client refunds + remaining = total`, with `gross awards = net freelancer transfers + platform fees`. The arbiter chooses a bounded gross award and whether to end the escrow, but cannot choose a different fee destination or rate.
