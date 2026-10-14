using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Blockchain;

public interface IEscrowDeploymentOrchestrator
{
    Task<bool> TryDeployAsync(
        Guid escrowId,
        CancellationToken cancellationToken);
}

public sealed class EscrowDeploymentOrchestrator(
    AppDbContext dbContext,
    IEscrowChainDeployer deployer,
    IOptions<BlockchainOptions> blockchainOptions)
    : IEscrowDeploymentOrchestrator
{
    public async Task<bool> TryDeployAsync(
        Guid escrowId,
        CancellationToken cancellationToken)
    {
        var current = await dbContext.Escrows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == escrowId,
                cancellationToken
            );

        if (current is null)
        {
            return false;
        }

        if (current.Status == EscrowStatus.Deploying)
        {
            if (string.IsNullOrWhiteSpace(
                    current.DeploymentTransactionHash))
            {
                throw new InvalidOperationException(
                    $"Escrow {escrowId} has an uncertain deployment " +
                    "without a saved transaction hash. Automatic " +
                    "redeployment is blocked to preserve one contract " +
                    "per project."
                );
            }

            return await FinalizeDeploymentAsync(
                current,
                current.DeploymentTransactionHash,
                cancellationToken
            );
        }

        var claimed = await dbContext.Escrows
            .Where(item =>
                item.Id == escrowId &&
                item.Status == EscrowStatus.PendingDeployment)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.Deploying
                    )
                    .SetProperty(
                        item => item.UpdatedAt,
                        DateTimeOffset.UtcNow
                    ),
                cancellationToken
            );

        if (claimed == 0)
        {
            return false;
        }

        string? submittedTransactionHash = null;

        try
        {
            var escrow = await LoadAndValidateEscrowAsync(
                escrowId,
                cancellationToken
            );

            var rpcChainId = await deployer.GetChainIdAsync(
                cancellationToken
            );

            if (rpcChainId != escrow.ChainId)
            {
                throw new InvalidOperationException(
                    $"RPC chain {rpcChainId} does not match " +
                    $"escrow chain {escrow.ChainId}."
                );
            }

            var request = await BuildDeploymentRequestAsync(
                escrow,
                cancellationToken
            );

            var submission = await deployer.SubmitDeploymentAsync(
                request,
                cancellationToken
            );

            submittedTransactionHash = submission.TransactionHash;

            var transactionSaved = await dbContext.Escrows
                .Where(item =>
                    item.Id == escrowId &&
                    item.Status == EscrowStatus.Deploying &&
                    item.DeploymentTransactionHash == null)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            item => item.DeploymentTransactionHash,
                            submission.TransactionHash
                        )
                        .SetProperty(
                            item => item.UpdatedAt,
                            submission.SubmittedAt
                        ),
                    cancellationToken
                );

            if (transactionSaved != 1)
            {
                throw new InvalidOperationException(
                    $"Deployment transaction " +
                    $"{submission.TransactionHash} could not be " +
                    $"saved for escrow {escrowId}."
                );
            }

            return await FinalizeDeploymentAsync(
                escrow,
                submission.TransactionHash,
                cancellationToken
            );
        }
        catch
        {
            if (submittedTransactionHash is null)
            {
                await dbContext.Escrows
                    .Where(item =>
                        item.Id == escrowId &&
                        item.Status == EscrowStatus.Deploying &&
                        item.DeploymentTransactionHash == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                item => item.Status,
                                EscrowStatus.PendingDeployment
                            )
                            .SetProperty(
                                item => item.UpdatedAt,
                                DateTimeOffset.UtcNow
                            ),
                        CancellationToken.None
                    );
            }
            else
            {
                await dbContext.Escrows
                    .Where(item =>
                        item.Id == escrowId &&
                        item.Status == EscrowStatus.Deploying &&
                        item.DeploymentTransactionHash == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                item => item.DeploymentTransactionHash,
                                submittedTransactionHash
                            )
                            .SetProperty(
                                item => item.UpdatedAt,
                                DateTimeOffset.UtcNow
                            ),
                        CancellationToken.None
                    );
            }

            throw;
        }
    }

    private async Task<Escrow> LoadAndValidateEscrowAsync(
        Guid escrowId,
        CancellationToken cancellationToken)
    {
        var escrow = await dbContext.Escrows
            .AsNoTracking()
            .FirstAsync(
                item => item.Id == escrowId,
                cancellationToken
            );

        if (string.IsNullOrWhiteSpace(escrow.ClientWalletAddress) ||
            string.IsNullOrWhiteSpace(escrow.FreelancerWalletAddress))
        {
            throw new InvalidOperationException(
                $"Escrow {escrow.Id} is missing a wallet " +
                "snapshot required for deployment."
            );
        }

        return escrow;
    }

    private async Task<EscrowDeploymentRequest> BuildDeploymentRequestAsync(
        Escrow escrow,
        CancellationToken cancellationToken)
    {
        if (escrow.PlatformFeeBasisPoints is not (0 or EscrowFeePolicy.BasisPoints) ||
            (escrow.PlatformFeeBasisPoints == 0) != (escrow.FeeRecipientAddress is null))
        {
            throw new InvalidOperationException("Invalid immutable escrow fee policy snapshot.");
        }

        // The contract pays one immutable address, so this escrow carries
        // only the milestones assigned to its own freelancer, in their own
        // sequence order.
        var milestoneAmounts = await dbContext.Milestones
            .Where(item =>
                item.ProjectId == escrow.ProjectId &&
                item.FreelancerId == escrow.FreelancerId)
            .OrderBy(item => item.SequenceNumber)
            .Select(item => item.Amount)
            .ToListAsync(cancellationToken);

        if (milestoneAmounts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Freelancer {escrow.FreelancerId} has no milestones " +
                $"on project {escrow.ProjectId} to deploy an escrow for."
            );
        }

        if (milestoneAmounts.Sum() != escrow.TotalAmount)
        {
            throw new InvalidOperationException(
                $"Milestone total for freelancer {escrow.FreelancerId} " +
                $"on project {escrow.ProjectId} does not match the " +
                "escrow's total amount."
            );
        }

        var tokenDecimals = await deployer.GetTokenDecimalsAsync(
            escrow.TokenAddress,
            cancellationToken
        );

        var baseUnitAmounts = milestoneAmounts
            .Select(amount => TokenAmountConverter.ToBaseUnits(
                amount,
                tokenDecimals
            ))
            .ToList();

        return new EscrowDeploymentRequest(
            escrow.TokenAddress,
            escrow.ClientWalletAddress!,
            escrow.FreelancerWalletAddress!,
            // An escrow keeps the arbiter it was deployed with, so a retry
            // after the setting changed still matches the contract.
            escrow.ArbiterWalletAddress ??
                blockchainOptions.Value.ArbiterAddress,
            baseUnitAmounts,
            escrow.FeeRecipientAddress,
            tokenDecimals
        );
    }

    private async Task<bool> FinalizeDeploymentAsync(
        Escrow escrow,
        string transactionHash,
        CancellationToken cancellationToken)
    {
        var result = await deployer.WaitForDeploymentAsync(
            transactionHash,
            cancellationToken
        );

        var request = await BuildDeploymentRequestAsync(
            escrow,
            cancellationToken
        );

        var snapshot = await deployer.GetEscrowSnapshotAsync(
            result.ContractAddress,
            cancellationToken
        );

        if (escrow.PlatformFeeBasisPoints > 0)
        {
            var feePolicy = await deployer.GetFeePolicyAsync(result.ContractAddress, cancellationToken);
            if (feePolicy.BasisPoints != escrow.PlatformFeeBasisPoints ||
                !string.Equals(feePolicy.Recipient, escrow.FeeRecipientAddress, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Deployed escrow fee policy does not match its immutable snapshot.");
        }

        var expectedTotal = request.MilestoneAmounts.Aggregate(
            (left, right) => left + right
        );

        if (!string.Equals(
                snapshot.Token,
                request.TokenAddress,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.Client,
                request.ClientAddress,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.Freelancer,
                request.FreelancerAddress,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.Arbiter,
                request.ArbiterAddress,
                StringComparison.OrdinalIgnoreCase) ||
            snapshot.TotalAmount != expectedTotal ||
            snapshot.State != OnChainEscrowState.AwaitingFunding ||
            snapshot.WorkStarted)
        {
            throw new InvalidOperationException(
                $"Deployment transaction {transactionHash} produced " +
                "a contract that does not match the escrow snapshot."
            );
        }

        var updated = await dbContext.Escrows
            .Where(item =>
                item.Id == escrow.Id &&
                item.Status == EscrowStatus.Deploying &&
                item.DeploymentTransactionHash == transactionHash)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.ContractAddress,
                        result.ContractAddress
                    )
                    .SetProperty(
                        item => item.TokenDecimals,
                        request.TokenDecimals
                    )
                    .SetProperty(
                        item => item.DeployedAt,
                        result.DeployedAt
                    )
                    .SetProperty(
                        item => item.ArbiterWalletAddress,
                        request.ArbiterAddress
                    )
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.AwaitingFunding
                    )
                    .SetProperty(
                        item => item.UpdatedAt,
                        DateTimeOffset.UtcNow
                    ),
                cancellationToken
            );

        if (updated == 1)
        {
            return true;
        }

        return await dbContext.Escrows
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id == escrow.Id &&
                    item.Status == EscrowStatus.AwaitingFunding &&
                    item.DeploymentTransactionHash == transactionHash &&
                    item.ContractAddress == result.ContractAddress,
                cancellationToken
            );
    }
}
