using System.Collections.Concurrent;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed class FakeEscrowChainDeployer : IEscrowChainDeployer
{
    public const int TokenDecimals = 6;

    // A new FakeEscrowChainDeployer is resolved per DI scope (per HTTP
    // request), but a test's "deploy" and later "sync" calls are separate
    // requests, so deployment details are tracked here so the fake can
    // simulate a consistent, already-funded on-chain snapshot.
    private static readonly ConcurrentDictionary<
        string,
        EscrowDeploymentRequest> DeployedEscrows = new();

    public Task<int> GetTokenDecimalsAsync(
        string tokenAddress,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(TokenDecimals);
    }

    public Task<EscrowDeploymentResult> DeployAsync(
        EscrowDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        // Real addresses/hashes are 20/32 raw bytes; build fake hex
        // strings of the exact same length from random bytes.
        var addressBytes = new byte[20];
        var hashBytes = new byte[32];

        Random.Shared.NextBytes(addressBytes);
        Random.Shared.NextBytes(hashBytes);

        var contractAddress =
            "0x" + Convert.ToHexStringLower(addressBytes);

        var transactionHash =
            "0x" + Convert.ToHexStringLower(hashBytes);

        DeployedEscrows[contractAddress] = request;

        return Task.FromResult(
            new EscrowDeploymentResult(
                contractAddress,
                transactionHash,
                DateTimeOffset.UtcNow
            )
        );
    }

    public Task<OnChainEscrowSnapshot> GetEscrowSnapshotAsync(
        string contractAddress,
        CancellationToken cancellationToken)
    {
        if (!DeployedEscrows.TryGetValue(
                contractAddress,
                out var request))
        {
            throw new InvalidOperationException(
                $"No fake deployment recorded for " +
                $"{contractAddress}."
            );
        }

        var totalBaseUnits = request.MilestoneAmounts
            .Aggregate((left, right) => left + right);

        return Task.FromResult(
            new OnChainEscrowSnapshot(
                request.TokenAddress,
                request.ClientAddress,
                request.FreelancerAddress,
                totalBaseUnits,
                OnChainEscrowState.Funded
            )
        );
    }
}
