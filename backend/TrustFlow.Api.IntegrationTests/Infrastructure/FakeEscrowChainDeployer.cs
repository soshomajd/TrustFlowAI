using System.Collections.Concurrent;
using TrustFlow.Api.Blockchain;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed class FakeEscrowChainDeployer : IEscrowChainDeployer
{
    public const int TokenDecimals = 6;

    private static readonly ConcurrentDictionary<
        string,
        EscrowDeploymentRequest> DeployedEscrows = new();

    private static readonly ConcurrentDictionary<
        string,
        EscrowDeploymentResult> DeploymentResults = new();

    private static readonly ConcurrentDictionary<
        (string ContractAddress, int MilestoneIndex),
        bool> ReleasedMilestones = new();

    private static readonly ConcurrentDictionary<string, bool>
        AwaitingDeploymentValidation = new();

    private static readonly ConcurrentDictionary<string, bool>
        StartedEscrows = new();

    private static readonly ConcurrentDictionary<string, bool>
        CancelledEscrows = new();

    public static void MarkMilestoneReleased(
        string contractAddress,
        int milestoneIndex)
    {
        ReleasedMilestones[(contractAddress, milestoneIndex)] = true;
    }

    public static void MarkWorkStarted(string contractAddress)
    {
        StartedEscrows[contractAddress] = true;
    }

    public static void MarkCancelled(string contractAddress)
    {
        CancelledEscrows[contractAddress] = true;
    }

    public Task<long> GetChainIdAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(31_337L);
    }

    public Task<int> GetTokenDecimalsAsync(
        string tokenAddress,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(TokenDecimals);
    }

    public Task<EscrowDeploymentSubmission> SubmitDeploymentAsync(
        EscrowDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var addressBytes = new byte[20];
        var hashBytes = new byte[32];

        Random.Shared.NextBytes(addressBytes);
        Random.Shared.NextBytes(hashBytes);

        var contractAddress =
            "0x" + Convert.ToHexStringLower(addressBytes);

        var transactionHash =
            "0x" + Convert.ToHexStringLower(hashBytes);

        var now = DateTimeOffset.UtcNow;

        DeployedEscrows[contractAddress] = request;
        DeploymentResults[transactionHash] = new EscrowDeploymentResult(
            contractAddress,
            transactionHash,
            now
        );

        return Task.FromResult(
            new EscrowDeploymentSubmission(transactionHash, now)
        );
    }

    public Task<EscrowDeploymentResult> WaitForDeploymentAsync(
        string transactionHash,
        CancellationToken cancellationToken)
    {
        if (!DeploymentResults.TryGetValue(
                transactionHash,
                out var result))
        {
            throw new InvalidOperationException(
                $"No fake deployment transaction recorded for " +
                $"{transactionHash}."
            );
        }

        AwaitingDeploymentValidation[result.ContractAddress] = true;
        return Task.FromResult(result);
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

        var isDeploymentValidation =
            AwaitingDeploymentValidation.TryRemove(
                contractAddress,
                out _
            );

        return Task.FromResult(
            new OnChainEscrowSnapshot(
                request.TokenAddress,
                request.ClientAddress,
                request.FreelancerAddress,
                totalBaseUnits,
                CancelledEscrows.ContainsKey(contractAddress)
                    ? OnChainEscrowState.Cancelled
                    : allReleased
                        ? OnChainEscrowState.Completed
                        : isDeploymentValidation
                            ? OnChainEscrowState.AwaitingFunding
                            : OnChainEscrowState.Funded,
                StartedEscrows.ContainsKey(contractAddress)
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
