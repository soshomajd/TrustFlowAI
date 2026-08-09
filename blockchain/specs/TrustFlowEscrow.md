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
