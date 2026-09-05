using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Blockchain;

public interface IEscrowDeploymentOrchestrator
{
    Task<bool> TryDeployAsync(
        Guid escrowId,
        CancellationToken cancellationToken);
}

public sealed class EscrowDeploymentOrchestrator(
    AppDbContext dbContext,
    IEscrowChainDeployer deployer)
    : IEscrowDeploymentOrchestrator
{
    public async Task<bool> TryDeployAsync(
        Guid escrowId,
        CancellationToken cancellationToken)
    {
        var claimed = await dbContext.Escrows
            .Where(item =>
                item.Id == escrowId &&
                item.Status == EscrowStatus.PendingDeployment)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    EscrowStatus.Deploying
                ),
                cancellationToken
            );

        if (claimed == 0)
        {
            return false;
        }

        try
        {
            var escrow = await dbContext.Escrows
                .AsNoTracking()
                .FirstAsync(
                    item => item.Id == escrowId,
                    cancellationToken
                );

            if (string.IsNullOrWhiteSpace(
                    escrow.ClientWalletAddress) ||
                string.IsNullOrWhiteSpace(
                    escrow.FreelancerWalletAddress))
            {
                throw new InvalidOperationException(
                    $"Escrow {escrow.Id} is missing a wallet " +
                    "snapshot required for deployment."
                );
            }

            var milestoneAmounts = await dbContext.Milestones
                .Where(item =>
                    item.ProjectId == escrow.ProjectId)
                .OrderBy(item => item.SequenceNumber)
                .Select(item => item.Amount)
                .ToListAsync(cancellationToken);

            if (milestoneAmounts.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Project {escrow.ProjectId} has no " +
                    "milestones to deploy an escrow for."
                );
            }

            if (milestoneAmounts.Sum() != escrow.TotalAmount)
            {
                throw new InvalidOperationException(
                    $"Milestone total for project " +
                    $"{escrow.ProjectId} does not match the " +
                    "escrow's total amount."
                );
            }

            var tokenDecimals = await deployer
                .GetTokenDecimalsAsync(
                    escrow.TokenAddress,
                    cancellationToken
                );

            var baseUnitAmounts = milestoneAmounts
                .Select(amount =>
                    TokenAmountConverter.ToBaseUnits(
                        amount,
                        tokenDecimals
                    ))
                .ToList();

            var result = await deployer.DeployAsync(
                new EscrowDeploymentRequest(
                    escrow.TokenAddress,
                    escrow.ClientWalletAddress,
                    escrow.FreelancerWalletAddress,
                    baseUnitAmounts
                ),
                cancellationToken
            );

            await dbContext.Escrows
                .Where(item => item.Id == escrowId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            item => item.ContractAddress,
                            result.ContractAddress
                        )
                        .SetProperty(
                            item => item.DeploymentTransactionHash,
                            result.TransactionHash
                        )
                        .SetProperty(
                            item => item.DeployedAt,
                            result.DeployedAt
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

            return true;
        }
        catch
        {
            await dbContext.Escrows
                .Where(item =>
                    item.Id == escrowId &&
                    item.Status == EscrowStatus.Deploying)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        item => item.Status,
                        EscrowStatus.PendingDeployment
                    ),
                    CancellationToken.None
                );

            throw;
        }
    }
}
