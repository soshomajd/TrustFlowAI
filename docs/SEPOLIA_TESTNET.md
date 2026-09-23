# Sepolia testnet rollout

TrustFlow uses Sepolia for its first public-chain staging environment. Local Hardhat remains the fast development and automated-test network.

## Safety rules

- Use a new deployer wallet made only for Sepolia. Never reuse a mainnet wallet or private key.
- The backend deployer pays only contract deployment gas. It never holds or signs for client or freelancer funds.
- Client and freelancer transactions stay user-signed in their own browser wallets.
- `MockUSDC` is a valueless test token with public `mint`. Never configure this contract on mainnet.
- Create fresh Sepolia projects. Existing local escrow addresses belong to chain 31337 and cannot move to Sepolia.

## 1. Prepare Sepolia access

Create an HTTPS Sepolia RPC endpoint with a provider. Put the RPC URL and the dedicated deployer private key in the Hardhat keystore from `blockchain/`:

```powershell
npx.cmd hardhat keystore set SEPOLIA_RPC_URL
npx.cmd hardhat keystore set SEPOLIA_PRIVATE_KEY
```

Fund the deployer, client, and freelancer test wallets with Sepolia ETH from a faucet listed by Ethereum.org:

- https://ethereum.org/developers/docs/networks/#sepolia

## 2. Validate contracts locally

```powershell
cd blockchain
npx.cmd hardhat compile
npx.cmd hardhat test nodejs
```

## 3. Deploy the test token

```powershell
npx.cmd hardhat ignition deploy ignition/modules/MockUSDCModule.ts --network sepolia --deployment-id sepolia-mock-usdc
```

Copy the deployed `MockUSDCModule#MockUSDC` address. Confirm it on https://sepolia.etherscan.io and keep it as the single payment-token address for the Sepolia environment.

## 4. Configure the backend

Set these values in the terminal that starts the API. Keep the real RPC URL and private key out of git.

```powershell
$env:Blockchain__ChainId="11155111"
$env:Blockchain__RpcUrl="YOUR_SEPOLIA_RPC_URL"
$env:Blockchain__TokenAddress="YOUR_SEPOLIA_MOCK_USDC_ADDRESS"
$env:Blockchain__DeployerPrivateKey="YOUR_DEDICATED_SEPOLIA_DEPLOYER_PRIVATE_KEY"
```

The API rejects an escrow whose requested chain or token differs from these values. The deployer also verifies the RPC chain before broadcasting.

## 5. Configure the frontend

Create `frontend/trustflow-web/.env.local` from `.env.example`, then use:

```dotenv
NEXT_PUBLIC_API_URL=http://localhost:5114/api
NEXT_PUBLIC_CHAIN_ID=11155111
NEXT_PUBLIC_CHAIN_NAME=Sepolia
NEXT_PUBLIC_RPC_URL=YOUR_SEPOLIA_RPC_URL
NEXT_PUBLIC_TOKEN_ADDRESS=YOUR_SEPOLIA_MOCK_USDC_ADDRESS
NEXT_PUBLIC_TOKEN_SYMBOL=mUSDC
NEXT_PUBLIC_BLOCK_EXPLORER_URL=https://sepolia.etherscan.io
NEXT_PUBLIC_ENABLE_TEST_TOKEN_MINT=true
```

Restart Next.js after changing any `NEXT_PUBLIC_` value.

## 6. Run one clean two-role test

1. Register one Client and one Freelancer.
2. Connect and verify a different Sepolia wallet for each account.
3. Create a fresh project and milestones, then accept the proposal.
4. Create and deploy the escrow. Check the deployment transaction on Etherscan.
5. Give the client Sepolia ETH, mint the missing mUSDC, approve, and fund the full budget.
6. Start work with the freelancer wallet.
7. Submit, review, release, and approve milestones in order.
8. On a second fresh project, fund and cancel before work starts. Confirm the refund and `Cancelled` status.
9. Reject wrong-network, wrong-wallet, insufficient-gas, rejected-wallet-transaction, refresh, and retry paths.

## Recovery behavior

The API stores the deployment transaction hash before waiting for the receipt. If the API restarts, the client can use **Check deployment** to continue the same transaction instead of deploying a second contract. Funding, cancellation, and released milestones are read from the contract and reconciled into backend state.
