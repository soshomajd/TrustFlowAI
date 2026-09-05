using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Blockchain;

public interface IEscrowFundingSyncOrchestrator
{
    Task<bool> TrySyncFundingAsync(
        Guid escrowId,
        CancellationToken cancellationToken);
}

public sealed class EscrowFundingSyncOrchestrator(
    AppDbContext dbContext,
    IEscrowChainDeployer deployer)
    : IEscrowFundingSyncOrchestrator
{
    public async Task<bool> TrySyncFundingAsync(
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
            escrow.Status != EscrowStatus.AwaitingFunding ||
            string.IsNullOrWhiteSpace(escrow.ContractAddress))
        {
            return false;
        }

        var snapshot = await deployer.GetEscrowSnapshotAsync(
            escrow.ContractAddress,
            cancellationToken
        );

        if (snapshot.State != OnChainEscrowState.Funded)
        {
            return false;
        }

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
                    await deployer.GetTokenDecimalsAsync(
                        escrow.TokenAddress,
                        cancellationToken
                    )
                ))
        {
            throw new InvalidOperationException(
                $"On-chain escrow {escrow.ContractAddress} does " +
                $"not match the backend snapshot for escrow " +
                $"{escrow.Id}."
            );
        }

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
                        DateTimeOffset.UtcNow
                    )
                    .SetProperty(
                        item => item.UpdatedAt,
                        DateTimeOffset.UtcNow
                    ),
                cancellationToken
            );

        return updated == 1;
    }
}
