using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace TrustFlow.Api.Blockchain;

public sealed record OnChainFundingEvent(string TransactionHash, string Client, BigInteger Amount);
public sealed record OnChainCancellationEvent(string TransactionHash, BigInteger RefundedAmount);
public sealed record OnChainLifecycleEvents(
    OnChainFundingEvent? Funding,
    OnChainCancellationEvent? Cancellation);

public sealed partial class EscrowChainDeployer
{
    // Read canonical contract logs, including transactions submitted outside
    // the site. Never accept a transaction hash as financial evidence from a caller.
    public async Task<OnChainLifecycleEvents> GetLifecycleEventsAsync(
        string contractAddress,
        string deploymentTransactionHash,
        bool includeFunding,
        bool includeCancellation,
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();
        var deployment = await web3.Eth.Transactions.GetTransactionReceipt
            .SendRequestAsync(deploymentTransactionHash).WaitAsync(cancellationToken);
        if (deployment?.Status?.Value != 1 || deployment.BlockNumber is null ||
            !string.Equals(deployment.ContractAddress, contractAddress, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Escrow deployment receipt is missing or does not match the contract.");
        }

        var latest = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync().WaitAsync(cancellationToken);
        var fundingEvent = web3.Eth.GetEvent<FundedEvent>(contractAddress);
        var cancellationEvent = web3.Eth.GetEvent<CancelledEvent>(contractAddress);
        OnChainFundingEvent? funding = null;
        OnChainCancellationEvent? cancellation = null;

        // Bound each RPC query to avoid provider range limits. Start at the
        // confirmed deployment block, not genesis, and honour request cancellation.
        for (var start = deployment.BlockNumber.Value; start <= latest.Value; start += 2_000)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var end = BigInteger.Min(start + 1_999, latest.Value);
            var from = new BlockParameter(new HexBigInteger(start));
            var to = new BlockParameter(new HexBigInteger(end));
            if (includeFunding)
            {
                var logs = await fundingEvent.GetAllChangesAsync(
                    fundingEvent.CreateFilterInput(from, to)).WaitAsync(cancellationToken);
                foreach (var log in logs.Where(item => item.Log.Removed != true))
                {
                    if (funding is not null)
                        throw new InvalidOperationException("Escrow emitted more than one funding event.");
                    funding = new OnChainFundingEvent(log.Log.TransactionHash, log.Event.Client, log.Event.Amount);
                }
            }
            if (includeCancellation)
            {
                var logs = await cancellationEvent.GetAllChangesAsync(
                    cancellationEvent.CreateFilterInput(from, to)).WaitAsync(cancellationToken);
                foreach (var log in logs.Where(item => item.Log.Removed != true))
                {
                    if (cancellation is not null)
                        throw new InvalidOperationException("Escrow emitted more than one cancellation event.");
                    cancellation = new OnChainCancellationEvent(log.Log.TransactionHash, log.Event.RefundedAmount);
                }
            }
        }

        return new OnChainLifecycleEvents(funding, cancellation);
    }

    [Event("Funded")]
    public sealed class FundedEvent : IEventDTO
    {
        [Parameter("address", "client", 1, true)]
        public string Client { get; set; } = string.Empty;
        [Parameter("uint256", "amount", 2, false)]
        public BigInteger Amount { get; set; }
    }

    [Event("EscrowCancelled")]
    public sealed class CancelledEvent : IEventDTO
    {
        [Parameter("uint256", "refundedAmount", 1, false)]
        public BigInteger RefundedAmount { get; set; }
    }
}
