using System.Numerics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Blockchain;

public sealed record EscrowDeploymentRequest(
    string TokenAddress,
    string ClientAddress,
    string FreelancerAddress,
    IReadOnlyList<BigInteger> MilestoneAmounts);

public sealed record EscrowDeploymentSubmission(
    string TransactionHash,
    DateTimeOffset SubmittedAt);

public sealed record EscrowDeploymentResult(
    string ContractAddress,
    string TransactionHash,
    DateTimeOffset DeployedAt);

public enum OnChainEscrowState
{
    AwaitingFunding,
    Funded,
    Completed,
    Cancelled
}

public sealed record OnChainEscrowSnapshot(
    string Token,
    string Client,
    string Freelancer,
    BigInteger TotalAmount,
    OnChainEscrowState State,
    bool WorkStarted);

public sealed record OnChainMilestone(
    BigInteger Amount,
    bool Released);

public interface IEscrowChainDeployer
{
    Task<long> GetChainIdAsync(
        CancellationToken cancellationToken);

    Task<int> GetTokenDecimalsAsync(
        string tokenAddress,
        CancellationToken cancellationToken);

    Task<EscrowDeploymentSubmission> SubmitDeploymentAsync(
        EscrowDeploymentRequest request,
        CancellationToken cancellationToken);

    Task<EscrowDeploymentResult> WaitForDeploymentAsync(
        string transactionHash,
        CancellationToken cancellationToken);

    Task<OnChainEscrowSnapshot> GetEscrowSnapshotAsync(
        string contractAddress,
        CancellationToken cancellationToken);

    Task<OnChainMilestone> GetMilestoneAsync(
        string contractAddress,
        int milestoneIndex,
        CancellationToken cancellationToken);
}

public sealed class EscrowChainDeployer(
    IOptions<BlockchainOptions> blockchainOptions)
    : IEscrowChainDeployer
{
    private const string Erc20DecimalsAbi =
        """
        [{"constant":true,"inputs":[],"name":"decimals","outputs":[{"name":"","type":"uint8"}],"payable":false,"stateMutability":"view","type":"function"}]
        """;

    private static readonly Lazy<(string Abi, string Bytecode)> Artifact =
        new(LoadArtifact);

    public async Task<long> GetChainIdAsync(
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();
        var chainId = await web3.Eth.ChainId.SendRequestAsync();
        return (long)chainId.Value;
    }

    public async Task<int> GetTokenDecimalsAsync(
        string tokenAddress,
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();

        var contract = web3.Eth.GetContract(
            Erc20DecimalsAbi,
            tokenAddress
        );

        var decimalsFunction = contract.GetFunction("decimals");

        return await decimalsFunction.CallAsync<byte>();
    }

    public async Task<EscrowDeploymentSubmission> SubmitDeploymentAsync(
        EscrowDeploymentRequest request,
        CancellationToken cancellationToken)
    {
        var (web3, deployerAddress) = CreateWeb3();

        var constructorParams = new TrustFlowEscrowConstructorParams
        {
            Token = request.TokenAddress,
            Client = request.ClientAddress,
            Freelancer = request.FreelancerAddress,
            MilestoneAmounts = request.MilestoneAmounts.ToList()
        };

        var estimatedGas = await web3.Eth.DeployContract.EstimateGasAsync(
            Artifact.Value.Bytecode,
            deployerAddress,
            constructorParams
        );

        var gasWithMargin = new HexBigInteger(
            estimatedGas.Value * 120 / 100
        );

        var transactionHash = await web3.Eth.DeployContract
            .SendRequestAsync(
                Artifact.Value.Bytecode,
                deployerAddress,
                gasWithMargin,
                constructorParams
            );

        return new EscrowDeploymentSubmission(
            transactionHash,
            DateTimeOffset.UtcNow
        );
    }

    public async Task<EscrowDeploymentResult> WaitForDeploymentAsync(
        string transactionHash,
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();

        var receipt = await web3.TransactionReceiptPolling
            .PollForReceiptAsync(
                transactionHash,
                cancellationToken
            );

        if (receipt.Status is null ||
            receipt.Status.Value != 1)
        {
            throw new InvalidOperationException(
                $"Escrow deployment transaction " +
                $"{transactionHash} reverted."
            );
        }

        if (string.IsNullOrWhiteSpace(receipt.ContractAddress))
        {
            throw new InvalidOperationException(
                $"Escrow deployment transaction " +
                $"{transactionHash} did not " +
                "produce a contract address."
            );
        }

        return new EscrowDeploymentResult(
            receipt.ContractAddress,
            receipt.TransactionHash,
            DateTimeOffset.UtcNow
        );
    }
    private (Web3 Web3, string DeployerAddress) CreateWeb3()
    {
        var options = blockchainOptions.Value;

        var account = new Account(
            options.DeployerPrivateKey,
            options.ChainId
        );

        var web3 = new Web3(account, options.RpcUrl, null, null);

        return (web3, account.Address);
    }

    public async Task<OnChainEscrowSnapshot> GetEscrowSnapshotAsync(
        string contractAddress,
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();

        var contract = web3.Eth.GetContract(
            Artifact.Value.Abi,
            contractAddress
        );

        var token = await contract.GetFunction("token")
            .CallAsync<string>();

        var client = await contract.GetFunction("client")
            .CallAsync<string>();

        var freelancer = await contract.GetFunction("freelancer")
            .CallAsync<string>();

        var totalAmount = await contract.GetFunction("totalAmount")
            .CallAsync<BigInteger>();

        var state = await contract.GetFunction("escrowState")
            .CallAsync<byte>();

        var workStarted = await contract.GetFunction("workStarted")
            .CallAsync<bool>();

        return new OnChainEscrowSnapshot(
            token,
            client,
            freelancer,
            totalAmount,
            (OnChainEscrowState)state,
            workStarted
        );
    }

    public async Task<OnChainMilestone> GetMilestoneAsync(
        string contractAddress,
        int milestoneIndex,
        CancellationToken cancellationToken)
    {
        var (web3, _) = CreateWeb3();

        var contract = web3.Eth.GetContract(
            Artifact.Value.Abi,
            contractAddress
        );

        var output = await contract
            .GetFunction("getMilestone")
            .CallDeserializingToObjectAsync<MilestoneOutput>(
                milestoneIndex
            );

        return new OnChainMilestone(
            output.Amount,
            output.Released
        );
    }

    private static (string Abi, string Bytecode) LoadArtifact()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Blockchain",
            "Artifacts",
            "TrustFlowEscrow.json"
        );

        using var stream = File.OpenRead(path);

        using var document = JsonDocument.Parse(stream);

        var abi = document.RootElement
            .GetProperty("abi")
            .GetRawText();

        var bytecode = document.RootElement
            .GetProperty("bytecode")
            .GetString()
            ?? throw new InvalidOperationException(
                "TrustFlowEscrow artifact is missing bytecode."
            );

        return (abi, bytecode);
    }

    private sealed class TrustFlowEscrowConstructorParams
    {
        [Parameter("address", "token_", 1)]
        public string Token { get; set; } = string.Empty;

        [Parameter("address", "client_", 2)]
        public string Client { get; set; } = string.Empty;

        [Parameter("address", "freelancer_", 3)]
        public string Freelancer { get; set; } = string.Empty;

        [Parameter("uint256[]", "milestoneAmounts_", 4)]
        public List<BigInteger> MilestoneAmounts { get; set; } = [];
    }

    [FunctionOutput]
    private sealed class MilestoneOutput : IFunctionOutputDTO
    {
        [Parameter("uint256", "amount", 1)]
        public BigInteger Amount { get; set; }

        [Parameter("bool", "released", 2)]
        public bool Released { get; set; }
    }
}
