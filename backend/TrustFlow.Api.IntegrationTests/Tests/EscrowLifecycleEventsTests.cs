using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nethereum.Util;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.IntegrationTests.Tests;

// Exercise the real Nethereum reader against a controlled RPC. The financial
// integration fake cannot catch ABI topic, decoding or block-range mistakes.
public sealed class EscrowLifecycleEventsTests
{
    private const string Contract = "0x1111111111111111111111111111111111111111";
    private const string Client = "0x2222222222222222222222222222222222222222";
    private const string DeploymentHash = "0x" + "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
    private const string FundingHash = "0x" + "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CancellationHash = "0x" + "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly string FundedTopic = "0x" + Sha3Keccack.Current.CalculateHash("Funded(address,uint256)");
    private static readonly string CancelledTopic = "0x" + Sha3Keccack.Current.CalculateHash("EscrowCancelled(uint256)");

    [Fact]
    public async Task Reader_DecodesBothEventsFromCanonicalLogsInBoundedRanges()
    {
        await using var rpc = new RpcServer(request =>
        {
            var method = request.GetProperty("method").GetString();
            if (method == "eth_getTransactionReceipt") return Receipt(Contract, "0x1");
            if (method == "eth_blockNumber") return "0xfc8"; // 4040
            var filter = request.GetProperty("params")[0];
            Assert.Equal(Contract, filter.GetProperty("address")[0].GetString());
            var start = Convert.ToInt32(filter.GetProperty("fromBlock").GetString(), 16);
            var end = Convert.ToInt32(filter.GetProperty("toBlock").GetString(), 16);
            Assert.InRange(end - start, 0, 1999);
            Assert.True(start >= 10);
            var topic = filter.GetProperty("topics")[0].GetString();
            if (start == 10 && topic == FundedTopic)
                return new[] { Log(FundingHash, FundedTopic, "0x" + Client[2..].PadLeft(64, '0'), false, 100_000_000) };
            if (start == 2010 && topic == CancelledTopic)
                return new[] { Log(CancellationHash, CancelledTopic, null, false, 100_000_000) };
            return Array.Empty<object>();
        });
        var result = await Reader(rpc.Url).GetLifecycleEventsAsync(
            Contract, DeploymentHash, true, true, CancellationToken.None);
        Assert.Equal(FundingHash, result.Funding!.TransactionHash);
        Assert.Equal(Client, result.Funding.Client, ignoreCase: true);
        Assert.Equal(new BigInteger(100_000_000), result.Funding.Amount);
        Assert.Equal(CancellationHash, result.Cancellation!.TransactionHash);
        Assert.Equal(new BigInteger(100_000_000), result.Cancellation.RefundedAmount);
        Assert.Equal(6, rpc.Methods.Count(item => item == "eth_getLogs"));
    }

    [Theory]
    [InlineData("0x3333333333333333333333333333333333333333", "0x1")]
    [InlineData(Contract, "0x0")]
    [InlineData(null, "0x1")]
    public async Task Reader_RejectsMissingFailedOrDifferentDeployment(string? contract, string status)
    {
        await using var rpc = new RpcServer(_ => contract is null ? null : Receipt(contract, status));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Reader(rpc.Url).GetLifecycleEventsAsync(
            Contract, DeploymentHash, true, true, CancellationToken.None));
        Assert.DoesNotContain("eth_getLogs", rpc.Methods);
    }

    [Fact]
    public async Task Reader_IgnoresRemovedLogsAndDoesNotInventFunding()
    {
        await using var rpc = new RpcServer(request => request.GetProperty("method").GetString() switch
        {
            "eth_getTransactionReceipt" => Receipt(Contract, "0x1"),
            "eth_blockNumber" => "0x14",
            _ => new[] { Log(FundingHash, FundedTopic, "0x" + Client[2..].PadLeft(64, '0'), true, 1) }
        });
        var result = await Reader(rpc.Url).GetLifecycleEventsAsync(
            Contract, DeploymentHash, true, false, CancellationToken.None);
        Assert.Null(result.Funding);
        Assert.Null(result.Cancellation);
    }

    [Fact]
    public async Task Reader_RejectsDuplicateFundingEvents()
    {
        await using var rpc = new RpcServer(request => request.GetProperty("method").GetString() switch
        {
            "eth_getTransactionReceipt" => Receipt(Contract, "0x1"),
            "eth_blockNumber" => "0x14",
            _ => new[]
            {
                Log(FundingHash, FundedTopic, "0x" + Client[2..].PadLeft(64, '0'), false, 1),
                Log(CancellationHash, FundedTopic, "0x" + Client[2..].PadLeft(64, '0'), false, 1)
            }
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => Reader(rpc.Url).GetLifecycleEventsAsync(
            Contract, DeploymentHash, true, false, CancellationToken.None));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData("0x4444444444444444444444444444444444444444", 6)]
    public async Task Deployment_EncodesTheCorrectConstructorArityBeforeBroadcast(string? feeRecipient, int words)
    {
        var artifactPath = Path.Combine(AppContext.BaseDirectory, "Blockchain", "Artifacts",
            feeRecipient is null ? "TrustFlowEscrow.json" : "TrustFlowEscrowV2.json");
        using var artifact = JsonDocument.Parse(await File.ReadAllTextAsync(artifactPath));
        var bytecode = artifact.RootElement.GetProperty("bytecode").GetString()!;
        var checkedEncoding = false;
        await using var rpc = new RpcServer(request =>
        {
            Assert.Equal("eth_estimateGas", request.GetProperty("method").GetString());
            var data = request.GetProperty("params")[0].GetProperty("data").GetString()!;
            Assert.StartsWith(bytecode.ToLowerInvariant(), data.ToLowerInvariant());
            var args = data[bytecode.Length..].ToLowerInvariant();
            Assert.Equal((words + 3) * 64, args.Length);
            Assert.Equal(Client[2..].PadLeft(64, '0'), args.Substring(64, 64));
            Assert.Equal((words * 32).ToString("x").PadLeft(64, '0'), args.Substring(4 * 64, 64));
            if (feeRecipient is not null)
                Assert.Equal(feeRecipient[2..].PadLeft(64, '0'), args.Substring(5 * 64, 64));
            Assert.Equal("2".PadLeft(64, '0'), args.Substring(words * 64, 64));
            Assert.Equal(50_000_000.ToString("x").PadLeft(64, '0'), args.Substring((words + 1) * 64, 64));
            checkedEncoding = true;
            // Stop before signing/broadcast: only constructor encoding is under test.
            return new RpcError(-32000, "Stop after checking constructor encoding");
        });
        await Assert.ThrowsAsync<Nethereum.JsonRpc.Client.RpcResponseException>(() =>
            Reader(rpc.Url).SubmitDeploymentAsync(new EscrowDeploymentRequest(
                Contract, Client, "0x3333333333333333333333333333333333333333",
                "0x5555555555555555555555555555555555555555",
                [50_000_000, 50_000_000], feeRecipient), CancellationToken.None));
        Assert.True(checkedEncoding);
        Assert.DoesNotContain("eth_sendRawTransaction", rpc.Methods);
    }

    private sealed record RpcError(int Code, string Message);

    private static object Receipt(string contract, string status) => new
    {
        transactionHash = DeploymentHash, blockNumber = "0xa", contractAddress = contract,
        status, logs = Array.Empty<object>()
    };

    private static object Log(string hash, string topic, string? indexedClient, bool removed, long amount) => new
    {
        address = Contract, transactionHash = hash, blockNumber = "0xf", logIndex = "0x0", transactionIndex = "0x0",
        blockHash = DeploymentHash, removed,
        topics = indexedClient is null ? new[] { topic } : new[] { topic, indexedClient },
        data = "0x" + amount.ToString("x").PadLeft(64, '0')
    };

    private static EscrowChainDeployer Reader(string url) => new(Microsoft.Extensions.Options.Options.Create(new BlockchainOptions
    {
        RpcUrl = url, ChainId = 31337,
        // A disposable reader account; never used to sign or send anything.
        DeployerPrivateKey = "0x" + new string('1', 64)
    }));

    private sealed class RpcServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly Task _loop;
        public string Url { get; }
        public ConcurrentQueue<string> Methods { get; } = new();

        public RpcServer(Func<JsonElement, object?> respond)
        {
            using var reserve = new TcpListener(IPAddress.Loopback, 0);
            reserve.Start();
            var port = ((IPEndPoint)reserve.LocalEndpoint).Port;
            reserve.Stop();
            Url = $"http://127.0.0.1:{port}/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _loop = Task.Run(async () =>
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch (Exception error) when (error is HttpListenerException or ObjectDisposedException) { break; }
                    using var request = await JsonDocument.ParseAsync(context.Request.InputStream);
                    var root = request.RootElement;
                    Methods.Enqueue(root.GetProperty("method").GetString()!);
                    object? result;
                    try { result = respond(root); }
                    catch
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                        throw;
                    }
                    var id = root.GetProperty("id").Clone();
                    object envelope = result is RpcError rpcError
                        ? new { jsonrpc = "2.0", id, error = new { code = rpcError.Code, message = rpcError.Message } }
                        : new { jsonrpc = "2.0", id, result };
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = bytes.Length;
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Close();
            await _loop;
        }
    }
}
