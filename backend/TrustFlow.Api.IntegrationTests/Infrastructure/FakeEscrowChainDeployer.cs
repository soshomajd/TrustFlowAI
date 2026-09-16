using System.Collections.Concurrent;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed class FakeEscrowChainDeployer : IEscrowChainDeployer
{
    public const int TokenDecimals = 6;

    // A new FakeEscrowChainDeployer is resolved per DI scope (per HTTP
    // request), but a test's "deploy", "sync", and "approve" calls are
    // separate requests, so deployment/release state is tracked here so
    // the fake can simulate a consistent on-chain contract across calls.
    private static readonly ConcurrentDictionary<
        string,
        EscrowDeploymentRequest> DeployedEscrows = new();

    private static readonly ConcurrentDictionary<
        (string ContractAddress, int MilestoneIndex),
        bool> ReleasedMilestones = new();

    // Test-only seam: the real contract only lets releaseMilestone() be
    // called by the client's own wallet, so there is no backend endpoint
    // that could ever do this. Tests call this directly to simulate that
    // wallet transaction having already been confirmed on-chain.
    public static void MarkMilestoneReleased(
        string contractAddress,
        int milestoneIndex)
    {
        ReleasedMilestones[(contractAddress, milestoneIndex)] = true;
    }

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
        var request = GetDeployedRequest(contractAddress);

        var totalBaseUnits = request.MilestoneAmounts
            .Aggregate((left, right) => left + right);

        var allReleased = Enumerable
            .Range(0, request.MilestoneAmounts.Count)
            .All(index =>
                ReleasedMilestones.ContainsKey(
                    (contractAddress, index)
                )
            );

        return Task.FromResult(
            new OnChainEscrowSnapshot(
                request.TokenAddress,
                request.ClientAddress,
                request.FreelancerAddress,
                totalBaseUnits,
                allReleased
                    ? OnChainEscrowState.Completed
                    : OnChainEscrowState.Funded
            )
        );
    }

    public Task<OnChainMilestone> GetMilestoneAsync(
        string contractAddress,
        int milestoneIndex,
        CancellationToken cancellationToken)
    {
        var request = GetDeployedRequest(contractAddress);

        var released = ReleasedMilestones.ContainsKey(
            (contractAddress, milestoneIndex)
        );

        return Task.FromResult(
            new OnChainMilestone(
                request.MilestoneAmounts[milestoneIndex],
                released
            )
        );
    }

    private static EscrowDeploymentRequest GetDeployedRequest(
        string contractAddress)
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

        return request;
    }
}
