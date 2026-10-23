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

    private static readonly ConcurrentDictionary<string, OnChainLifecycleEvents>
        LifecycleOverrides = new();
    private static readonly ConcurrentDictionary<string, IReadOnlyList<OnChainExecutionEvent>> ExecutionOverrides = new();
    private static readonly ConcurrentDictionary<(string Contract, int Index), DateTimeOffset> ExecutionTimes = new();
    public static void SetExecutionEvents(string contract, IReadOnlyList<OnChainExecutionEvent> events) => ExecutionOverrides[contract] = events;

    public Task<IReadOnlyList<OnChainExecutionEvent>> GetExecutionEventsAsync(string contractAddress, string deploymentTransactionHash, CancellationToken cancellationToken)
    {
        if (ExecutionOverrides.TryGetValue(contractAddress, out var overridden)) return Task.FromResult(overridden);
        var request = GetDeployedRequest(contractAddress);
        var result = new List<OnChainExecutionEvent>();
        void Add(string kind, int index, BigInteger paid, BigInteger refunded, string participant, string sender)
        {
            var hash = "0x" + (index + 2).ToString("x").PadLeft(24, '0') + contractAddress[2..];
            var at = ExecutionTimes.GetOrAdd((contractAddress, index), _ => DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            result.Add(new OnChainExecutionEvent(kind, index, paid, refunded, participant, hash, hash, index + 2, 0, at, sender));
        }
        if (StartedEscrows.ContainsKey(contractAddress)) Add("WorkStarted", -1, 0, 0, request.FreelancerAddress, request.FreelancerAddress);
        for (var index = 0; index < request.MilestoneAmounts.Count; index++)
        {
            if (SettledMilestones.TryGetValue((contractAddress, index), out var paid)) Add("CaseSettlement", index, paid, request.MilestoneAmounts[index] - paid, request.ArbiterAddress, request.ArbiterAddress);
            else if (ReleasedMilestones.ContainsKey((contractAddress, index))) Add("ClientRelease", index, request.MilestoneAmounts[index], 0, request.FreelancerAddress, request.ClientAddress);
        }
        return Task.FromResult<IReadOnlyList<OnChainExecutionEvent>>(result);
    }

    public static void SetLifecycleEvents(string contractAddress, OnChainLifecycleEvents events)
        => LifecycleOverrides[contractAddress] = events;

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

    public static void MarkCancelled(string contractAddress, bool wasFunded = true)
    {
        CancelledEscrows[contractAddress] = wasFunded;
    }

    public Task<long> GetChainIdAsync(
        CancellationToken cancellationToken)
    {
        return Task.FromResult(31_337L);
    }

    public Task<OnChainLifecycleEvents> GetLifecycleEventsAsync(
        string contractAddress,
        string deploymentTransactionHash,
        bool includeFunding,
        bool includeCancellation,
        CancellationToken cancellationToken)
    {
        if (LifecycleOverrides.TryGetValue(contractAddress, out var overridden))
            return Task.FromResult(overridden);
        var request = GetDeployedRequest(contractAddress);
        var amount = request.MilestoneAmounts.Aggregate(BigInteger.Zero, (sum, item) => sum + item);
        var cancelled = CancelledEscrows.TryGetValue(contractAddress, out var wasFunded);
        return Task.FromResult(new OnChainLifecycleEvents(
            includeFunding && (!cancelled || wasFunded)
                ? new OnChainFundingEvent("0x" + new string('1', 24) + contractAddress[2..], request.ClientAddress, amount)
                : null,
            includeCancellation && cancelled
                ? new OnChainCancellationEvent("0x" + new string('2', 24) + contractAddress[2..], wasFunded ? amount : BigInteger.Zero)
                : null));
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
                CancelledEscrows.TryGetValue(contractAddress, out var wasFunded) && wasFunded
                    ? totalBaseUnits
                    : TerminatedEscrows.ContainsKey(contractAddress)
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
        CancellationToken cancellationToken,
        bool withPlatformFee = false)
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
                settled,
                withPlatformFee ? (settled ? settledPaid : released ? amount : BigInteger.Zero) / 10 : BigInteger.Zero
            )
        );
    }

    public Task<OnChainFeePolicy> GetFeePolicyAsync(string contractAddress, CancellationToken cancellationToken)
    {
        var request = GetDeployedRequest(contractAddress);
        return Task.FromResult(new OnChainFeePolicy(request.FeeRecipientAddress ?? string.Empty, request.FeeRecipientAddress is null ? 0 : 1000));
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
