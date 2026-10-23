using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace TrustFlow.Api.Blockchain;

public sealed record OnChainExecutionEvent(string Kind, int MilestoneIndex, BigInteger GrossPaid,
    BigInteger Refunded, string Participant, string TransactionHash, string BlockHash,
    long BlockNumber, int LogIndex, DateTimeOffset OccurredAt, string TransactionSender);

public sealed partial class EscrowChainDeployer
{
    public async Task<IReadOnlyList<OnChainExecutionEvent>> GetExecutionEventsAsync(string contractAddress,
        string deploymentTransactionHash, CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();
        var deployment = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(deploymentTransactionHash).WaitAsync(cancellationToken);
        if (deployment?.Status?.Value != 1 || deployment.BlockNumber is null ||
            !string.Equals(deployment.ContractAddress, contractAddress, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Missing or mismatched deployment evidence for execution history.");
        var latest = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().WaitAsync(cancellationToken);
        var work = web3.Eth.GetEvent<StartedEvent>(contractAddress);
        var release = web3.Eth.GetEvent<ReleasedEvent>(contractAddress);
        var settle = web3.Eth.GetEvent<SettledEvent>(contractAddress);
        var result = new List<OnChainExecutionEvent>();
        var identities = new HashSet<int>();
        var receipts = new Dictionary<string, TransactionReceipt>(StringComparer.OrdinalIgnoreCase);
        var blocks = new Dictionary<string, BlockWithTransactionHashes>(StringComparer.OrdinalIgnoreCase);

        async Task Add(FilterLog log, string kind, BigInteger index, BigInteger paid, BigInteger refunded, string participant)
        {
            if (log.Removed == true) return;
            if (log.BlockNumber is null || log.LogIndex is null || index < -1 || index > int.MaxValue || paid < 0 || refunded < 0 ||
                !string.Equals(log.Address, contractAddress, StringComparison.OrdinalIgnoreCase) || !identities.Add((int)index))
                throw new InvalidOperationException("Invalid or duplicate execution event.");
            if (!receipts.TryGetValue(log.TransactionHash, out var receipt))
            {
                receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(log.TransactionHash).WaitAsync(cancellationToken);
                if (receipt is null) throw new InvalidOperationException("Execution receipt is missing.");
                receipts.Add(log.TransactionHash, receipt);
            }
            if (!blocks.TryGetValue(log.BlockHash, out var block))
            {
                block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(new BlockParameter(log.BlockNumber)).WaitAsync(cancellationToken);
                if (block is null) throw new InvalidOperationException("Execution block is missing.");
                blocks.Add(log.BlockHash, block);
            }
            if (receipt.Status?.Value != 1 || receipt.BlockNumber?.Value != log.BlockNumber.Value ||
                !string.Equals(receipt.TransactionHash, log.TransactionHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(receipt.BlockHash, log.BlockHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(block.BlockHash, log.BlockHash, StringComparison.OrdinalIgnoreCase) ||
                block.Timestamp is null || block.Number?.Value != log.BlockNumber.Value ||
                log.BlockNumber.Value < deployment.BlockNumber.Value || log.BlockNumber.Value > latest.Value)
                throw new InvalidOperationException("Execution event is not backed by a successful canonical receipt and block.");
            result.Add(new OnChainExecutionEvent(kind, (int)index, paid, refunded, participant, log.TransactionHash,
                log.BlockHash, checked((long)log.BlockNumber.Value), checked((int)log.LogIndex.Value),
                DateTimeOffset.FromUnixTimeSeconds(checked((long)block.Timestamp.Value)), receipt.From));
        }

        for (var start = deployment.BlockNumber.Value; start <= latest.Value; start += 2000)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var from = new BlockParameter(new HexBigInteger(start));
            var to = new BlockParameter(new HexBigInteger(BigInteger.Min(start + 1999, latest.Value)));
            foreach (var log in await work.GetAllChangesAsync(work.CreateFilterInput(from, to)).WaitAsync(cancellationToken))
                await Add(log.Log, "WorkStarted", -1, 0, 0, log.Event.Freelancer);
            foreach (var log in await release.GetAllChangesAsync(release.CreateFilterInput(from, to)).WaitAsync(cancellationToken))
                await Add(log.Log, "ClientRelease", log.Event.Index, log.Event.Amount, 0, log.Event.Freelancer);
            foreach (var log in await settle.GetAllChangesAsync(settle.CreateFilterInput(from, to)).WaitAsync(cancellationToken))
                await Add(log.Log, "CaseSettlement", log.Event.Index, log.Event.Paid, log.Event.Returned, log.Event.SettledBy);
        }
        return result;
    }

    [Event("WorkStarted")]
    public sealed class StartedEvent : IEventDTO
    { [Parameter("address", "freelancer", 1, true)] public string Freelancer { get; set; } = ""; }
    [Event("MilestoneReleased")]
    public sealed class ReleasedEvent : IEventDTO
    {
        [Parameter("uint256", "milestoneIndex", 1, true)] public BigInteger Index { get; set; }
        [Parameter("uint256", "amount", 2, false)] public BigInteger Amount { get; set; }
        [Parameter("address", "freelancer", 3, true)] public string Freelancer { get; set; } = "";
    }
    [Event("CaseSettled")]
    public sealed class SettledEvent : IEventDTO
    {
        [Parameter("uint256", "milestoneIndex", 1, true)] public BigInteger Index { get; set; }
        [Parameter("uint256", "toFreelancer", 2, false)] public BigInteger Paid { get; set; }
        [Parameter("uint256", "toClient", 3, false)] public BigInteger Returned { get; set; }
        [Parameter("bool", "escrowEnded", 4, false)] public bool Ended { get; set; }
        [Parameter("address", "settledBy", 5, true)] public string SettledBy { get; set; } = "";
    }
}
