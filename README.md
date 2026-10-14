# TrustFlowAI

A fixed-price freelance marketplace with one non-upgradeable escrow per freelancer per project. Clients choose milestone prices and release completed work; freelancers start work using their own verified wallets. A human arbiter can settle an open case after the waiting period. AI advice does not control funds.

New V2 escrows take a fixed 10% fee from gross freelancer awards. Client refunds have no fee. Existing V1 contracts keep their original terms.

## Repository

- [Backend](backend/): .NET 10 REST API, Identity, EF Core/PostgreSQL and integration tests.
- [Web client](frontend/trustflow-web/README.md): Next.js 16, role workspaces, chat and user-signed wallet actions.
- [Contracts](blockchain/README.md): Hardhat 3, viem, Solidity and local test token.
- [Project state](docs/PROJECT_STATE.md): decisions, validation and the next acceptance step.
- [Repository instructions](AGENTS.md): subsystem boundaries and security rules.

## Local workflow

Start local PostgreSQL and ClamAV, then Hardhat and MockUSDC. Configure backend runtime values through environment variables or .NET user secrets: database connection, JWT key, deployment account, public arbiter and public fee receiving wallet. The deployment account only deploys contracts; participant and arbiter keys remain in their wallets. An unset mail host in Development logs recovery/verification emails locally. AI defaults to labelled Mock answers.

From the repository root:

```bash
dotnet restore backend/TrustFlow.slnx
dotnet build backend/TrustFlow.slnx --no-restore
dotnet ef database update --project backend/TrustFlow.Api
dotnet run --project backend/TrustFlow.Api --launch-profile http
```

Then follow the web client's README and use `http://localhost:3000`. For two-role tests, use separate browser profiles. A Hardhat restart loses on-chain data while PostgreSQL keeps its records; restore the token and use a fresh project without deleting financial history.

## Validation

Integration tests require an isolated PostgreSQL database configured in `TRUSTFLOW_TEST_DB`; the test host applies migrations and retains its test data. Never point them at the development/shared database.

```bash
dotnet test backend/TrustFlow.Api.IntegrationTests/TrustFlow.Api.IntegrationTests.csproj
```

Run frontend lint/TypeScript checks and contract tests from their own directories. The beginner guides in both role dashboards explain the current user flow. Public deployment is deferred until local completion and acceptance.
