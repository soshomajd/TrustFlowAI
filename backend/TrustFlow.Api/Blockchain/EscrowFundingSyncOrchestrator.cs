using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Blockchain;

public interface IEscrowChainSyncOrchestrator
{
    Task<bool> TrySyncAsync(
        Guid escrowId,
        CancellationToken cancellationToken);
}

public sealed class EscrowChainSyncOrchestrator(
    AppDbContext dbContext,
    IEscrowChainDeployer deployer)
    : IEscrowChainSyncOrchestrator
{
    public async Task<bool> TrySyncAsync(
        Guid escrowId,
        CancellationToken cancellationToken)
    {
        var escrow = await dbContext.Escrows
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == escrowId,
                cancellationToken
            );

        if (escrow is null ||
            string.IsNullOrWhiteSpace(escrow.ContractAddress) ||
            escrow.Status is EscrowStatus.PendingDeployment or
                EscrowStatus.Deploying)
        {
            return false;
        }

        var snapshot = await deployer.GetEscrowSnapshotAsync(
            escrow.ContractAddress,
            cancellationToken
        );

        var tokenDecimals = await deployer.GetTokenDecimalsAsync(
            escrow.TokenAddress,
            cancellationToken
        );

        if (!string.Equals(
                snapshot.Token,
                escrow.TokenAddress,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.Client,
                escrow.ClientWalletAddress,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                snapshot.Freelancer,
                escrow.FreelancerWalletAddress,
                StringComparison.OrdinalIgnoreCase) ||
            snapshot.TotalAmount !=
                TokenAmountConverter.ToBaseUnits(
                    escrow.TotalAmount,
                    tokenDecimals
                ))
        {
            throw new InvalidOperationException(
                $"On-chain escrow {escrow.ContractAddress} does " +
                $"not match the backend snapshot for escrow " +
                $"{escrow.Id}."
            );
        }

        return snapshot.State switch
        {
            OnChainEscrowState.AwaitingFunding => false,
            OnChainEscrowState.Funded =>
                await SyncFundedAsync(escrowId, cancellationToken),
            OnChainEscrowState.Cancelled =>
                await SyncCancelledAsync(
                    escrowId,
                    escrow.ProjectId,
                    cancellationToken
                ),
            OnChainEscrowState.Completed => false,
            _ => throw new InvalidOperationException(
                $"Unsupported on-chain escrow state " +
                $"{snapshot.State}."
            )
        };
    }

    private async Task<bool> SyncFundedAsync(
        Guid escrowId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var updated = await dbContext.Escrows
            .Where(item =>
                item.Id == escrowId &&
                item.Status == EscrowStatus.AwaitingFunding)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.Funded
                    )
                    .SetProperty(
                        item => item.FundedAt,
                        now
                    )
                    .SetProperty(
                        item => item.UpdatedAt,
                        now
                    ),
                cancellationToken
            );

        return updated == 1;
    }

    private async Task<bool> SyncCancelledAsync(
        Guid escrowId,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var escrowUpdated = await dbContext.Escrows
            .Where(item =>
                item.Id == escrowId &&
                (item.Status == EscrowStatus.AwaitingFunding ||
                 item.Status == EscrowStatus.Funded))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.Cancelled
                    )
                    .SetProperty(
                        item => item.CancelledAt,
                        now
                    )
                    .SetProperty(
                        item => item.UpdatedAt,
                        now
                    ),
                cancellationToken
            );

        if (escrowUpdated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await dbContext.Projects
            .Where(item =>
                item.Id == projectId &&
                item.Status == ProjectStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    ProjectStatus.Cancelled
                ),
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
