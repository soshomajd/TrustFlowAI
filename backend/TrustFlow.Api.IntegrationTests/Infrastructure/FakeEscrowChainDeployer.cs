using System.Collections.Concurrent;
using System.Numerics;
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

    private sealed record FakeCase(
        OnChainCaseKind Kind,
        DateTimeOffset OpenedAt);

    private static readonly ConcurrentDictionary<string, FakeCase>
        OpenCases = new();

    // What the freelancer received for a milestone the arbiter closed.
    private static readonly ConcurrentDictionary<
        (string ContractAddress, int MilestoneIndex),
        BigInteger> SettledMilestones = new();

    private static readonly ConcurrentDictionary<string, bool>
        TerminatedEscrows = new();

    public static void OpenCase(
        string contractAddress,
        OnChainCaseKind kind,
        DateTimeOffset openedAt)
    {
        OpenCases[contractAddress] = new FakeCase(kind, openedAt);
    }

    public static void WithdrawCase(string contractAddress)
    {
        OpenCases.TryRemove(contractAddress, out _);
    }

    public static void SettleMilestone(
        string contractAddress,
        int milestoneIndex,
        BigInteger paidBaseUnits,
        bool endEscrow)
    {
        SettledMilestones[(contractAddress, milestoneIndex)] =
            paidBaseUnits;

        OpenCases.TryRemove(contractAddress, out _);

        // Like the contract: settling the last stage completes the escrow
        // whatever was asked, there is nothing left to end.
        if (endEscrow &&
            milestoneIndex + 1 < DeployedEscrows[contractAddress]
                .MilestoneAmounts.Count)
        {
            TerminatedEscrows[contractAddress] = true;
        }
    }

    public static void Resign(string contractAddress)
    {
        OpenCases.TryRemove(contractAddress, out _);
        TerminatedEscrows[contractAddress] = true;
    }

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

        bool IsClosed(int index) =>
            ReleasedMilestones.ContainsKey((contractAddress, index)) ||
            SettledMilestones.ContainsKey((contractAddress, index));

        var allReleased = Enumerable
            .Range(0, request.MilestoneAmounts.Count)
            .All(IsClosed);

        var nextIndex = 0;

        while (nextIndex < request.MilestoneAmounts.Count &&
               IsClosed(nextIndex))
        {
            nextIndex++;
        }

        var released = Enumerable
            .Range(0, nextIndex)
            .Aggregate(
                BigInteger.Zero,
                (sum, index) => sum + (
                    SettledMilestones.TryGetValue(
                        (contractAddress, index),
                        out var paid)
                        ? paid
                        : request.MilestoneAmounts[index]));

        OpenCases.TryGetValue(contractAddress, out var openCase);

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
                    : TerminatedEscrows.ContainsKey(contractAddress)
                        ? OnChainEscrowState.Terminated
                    : allReleased
                        ? OnChainEscrowState.Completed
                        : isDeploymentValidation
                            ? OnChainEscrowState.AwaitingFunding
                            : OnChainEscrowState.Funded,
                StartedEscrows.ContainsKey(contractAddress),
                request.ArbiterAddress,
                released,
                TerminatedEscrows.ContainsKey(contractAddress)
                    ? totalBaseUnits - released
                    : BigInteger.Zero,
                nextIndex,
                openCase?.Kind ?? OnChainCaseKind.None,
                openCase?.OpenedAt
            )
        );
    }

    public Task<OnChainMilestone> GetMilestoneAsync(
        string contractAddress,
        int milestoneIndex,
        CancellationToken cancellationToken)
    {
        var request = GetDeployedRequest(contractAddress);

        var settled = SettledMilestones.TryGetValue(
            (contractAddress, milestoneIndex),
            out var settledPaid
        );

        var released = settled || ReleasedMilestones.ContainsKey(
            (contractAddress, milestoneIndex)
        );

        var amount = request.MilestoneAmounts[milestoneIndex];

        return Task.FromResult(
            new OnChainMilestone(
                amount,
                released,
                settled
                    ? settledPaid
                    : released ? amount : BigInteger.Zero,
                settled
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
