using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace TrustFlow.Api.Blockchain;

public sealed record OnChainStageEvent(string Kind, int Index, string FundingReference,
    string TermsHash, BigInteger Amount, string TransactionHash, string BlockHash,
    long BlockNumber, int LogIndex, DateTimeOffset OccurredAt, string Sender);

public sealed partial class EscrowChainDeployer
{
    public async Task<IReadOnlyList<OnChainStageEvent>> GetStageEventsAsync(string contractAddress,
        string deploymentTransactionHash, CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();
        var deployment = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(deploymentTransactionHash).WaitAsync(cancellationToken);
        if (deployment?.Status?.Value != 1 || deployment.BlockNumber is null ||
            !SameAddress(deployment.ContractAddress, contractAddress))
            throw new InvalidOperationException("Staged funding requires a confirmed deployment.");
        var latest = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().WaitAsync(cancellationToken);
        var funding = web3.Eth.GetEvent<StageFundedEvent>(contractAddress);
        var started = web3.Eth.GetEvent<StageStartedEvent>(contractAddress);
        var result = new List<OnChainStageEvent>();
        var keys = new HashSet<(string, int)>();

        async Task Add(FilterLog log, string kind, BigInteger index, byte[] reference, byte[] terms, BigInteger amount)
        {
            if (log.Removed == true) return;
            if (index < 0 || index > int.MaxValue || log.BlockNumber is null || log.LogIndex is null ||
                reference.Length != 32 || terms.Length != 32 || !SameAddress(log.Address, contractAddress) ||
                !keys.Add((kind, (int)index))) throw new InvalidOperationException("Invalid or duplicate stage evidence.");
            var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(log.TransactionHash).WaitAsync(cancellationToken);
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(log.BlockNumber)).WaitAsync(cancellationToken);
            if (receipt?.Status?.Value != 1 || !SameAddress(receipt.BlockHash, log.BlockHash) ||
                receipt.BlockNumber?.Value != log.BlockNumber.Value || !SameAddress(block?.BlockHash, log.BlockHash) ||
                block?.Timestamp is null || block.Timestamp.Value <= 0 || log.BlockNumber.Value < deployment.BlockNumber.Value ||
                log.BlockNumber.Value > latest.Value)
                throw new InvalidOperationException("Stage evidence has no successful canonical receipt.");
            result.Add(new(kind, (int)index, "0x" + Convert.ToHexString(reference).ToLowerInvariant(),
                "0x" + Convert.ToHexString(terms).ToLowerInvariant(), amount, log.TransactionHash, log.BlockHash,
                checked((long)log.BlockNumber.Value), checked((int)log.LogIndex.Value),
                DateTimeOffset.FromUnixTimeSeconds(checked((long)block.Timestamp.Value)), receipt.From));
        }

        for (var from = deployment.BlockNumber.Value; from <= latest.Value; from += 2000)
        {
            var start = new BlockParameter(new HexBigInteger(from));
            var end = new BlockParameter(new HexBigInteger(BigInteger.Min(from + 1999, latest.Value)));
            foreach (var log in await funding.GetAllChangesAsync(funding.CreateFilterInput(start, end)).WaitAsync(cancellationToken))
                await Add(log.Log, "Funding", log.Event.Index, log.Event.Reference, log.Event.Terms, log.Event.Amount);
            foreach (var log in await started.GetAllChangesAsync(started.CreateFilterInput(start, end)).WaitAsync(cancellationToken))
                await Add(log.Log, "Start", log.Event.Index, log.Event.Reference, log.Event.Terms, 0);
        }
        return result;
    }

    private static bool SameAddress(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    [Event("MilestoneFunded")]
    public sealed class StageFundedEvent : IEventDTO
    {
        [Parameter("uint256", "milestoneIndex", 1, true)] public BigInteger Index { get; set; }
        [Parameter("bytes32", "fundingReference", 2, true)] public byte[] Reference { get; set; } = [];
        [Parameter("bytes32", "termsHash", 3, false)] public byte[] Terms { get; set; } = [];
        [Parameter("uint256", "amount", 4, false)] public BigInteger Amount { get; set; }
    }
    [Event("MilestoneWorkStarted")]
    public sealed class StageStartedEvent : IEventDTO
    {
        [Parameter("uint256", "milestoneIndex", 1, true)] public BigInteger Index { get; set; }
        [Parameter("bytes32", "fundingReference", 2, true)] public byte[] Reference { get; set; } = [];
        [Parameter("bytes32", "termsHash", 3, false)] public byte[] Terms { get; set; } = [];
    }
}
