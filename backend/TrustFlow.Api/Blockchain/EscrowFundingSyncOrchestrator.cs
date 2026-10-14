using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
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

        if (await deployer.GetChainIdAsync(cancellationToken) != escrow.ChainId)
            throw new InvalidOperationException("Escrow belongs to a different chain than the connected RPC.");

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

        if (escrow.ArbiterWalletAddress is not null &&
            !string.Equals(
                snapshot.Arbiter,
                escrow.ArbiterWalletAddress,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"On-chain escrow {escrow.ContractAddress} has a " +
                $"different arbiter than escrow {escrow.Id}."
            );
        }

        if (escrow.PlatformFeeBasisPoints > 0)
        {
            var policy = await deployer.GetFeePolicyAsync(escrow.ContractAddress, cancellationToken);
            if (policy.BasisPoints != escrow.PlatformFeeBasisPoints ||
                !string.Equals(policy.Recipient, escrow.FeeRecipientAddress, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("On-chain escrow fee policy differs from the stored snapshot.");
        }

        var traced = await SyncLifecycleEventsAsync(escrow, snapshot, cancellationToken);

        switch (snapshot.State)
        {
            case OnChainEscrowState.AwaitingFunding:
                return false;

            case OnChainEscrowState.Funded:
            {
                var funded = await SyncFundedAsync(
                    escrowId,
                    cancellationToken
                );

                var progressed = await SyncProgressAsync(
                    escrow,
                    snapshot,
                    tokenDecimals,
                    cancellationToken
                );

                return funded || progressed || traced;
            }

            case OnChainEscrowState.Cancelled:
                var cancelled = await SyncCancelledAsync(
                    escrowId,
                    escrow.ProjectId,
                    escrow.FreelancerId,
                    cancellationToken
                );
                return cancelled || traced;

            case OnChainEscrowState.Completed:
            case OnChainEscrowState.Terminated:
                var terminalProgress = await SyncProgressAsync(
                    escrow,
                    snapshot,
                    tokenDecimals,
                    cancellationToken
                );
                return terminalProgress || traced;

            default:
                throw new InvalidOperationException(
                    $"Unsupported on-chain escrow state " +
                    $"{snapshot.State}."
                );
        }
    }

    private async Task<bool> SyncLifecycleEventsAsync(
        Escrow escrow,
        OnChainEscrowSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.State == OnChainEscrowState.AwaitingFunding)
            return false;

        var needsFunding = escrow.FundingTransactionHash is null &&
            !(snapshot.State == OnChainEscrowState.Cancelled && snapshot.RefundedAmount == 0 &&
              escrow.CancellationTransactionHash is not null);
        var needsCancellation = snapshot.State == OnChainEscrowState.Cancelled &&
            escrow.CancellationTransactionHash is null;
        if (!needsFunding && !needsCancellation) return false;
        if (string.IsNullOrWhiteSpace(escrow.DeploymentTransactionHash))
            throw new InvalidOperationException("Escrow has no deployment transaction to trace.");

        var events = await deployer.GetLifecycleEventsAsync(
            escrow.ContractAddress!, escrow.DeploymentTransactionHash,
            needsFunding, needsCancellation, cancellationToken);
        if (events.Funding is { } funding &&
            (funding.Amount != snapshot.TotalAmount ||
             !string.Equals(funding.Client, escrow.ClientWalletAddress, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Funding event does not match the escrow amount and client.");
        if (needsFunding && events.Funding is null &&
            (snapshot.State != OnChainEscrowState.Cancelled || snapshot.RefundedAmount > 0))
            throw new InvalidOperationException("Funded escrow has no matching funding event.");
        if (needsCancellation && (events.Cancellation is null ||
            events.Cancellation.RefundedAmount != snapshot.RefundedAmount))
            throw new InvalidOperationException("Cancelled escrow has no matching refund event.");

        var changed = 0;
        if (needsFunding && events.Funding is not null)
            changed += await dbContext.Escrows
                .Where(item => item.Id == escrow.Id && item.FundingTransactionHash == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FundingTransactionHash, events.Funding.TransactionHash), cancellationToken);
        if (needsCancellation && events.Cancellation is not null)
            changed += await dbContext.Escrows
                .Where(item => item.Id == escrow.Id && item.CancellationTransactionHash == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.CancellationTransactionHash, events.Cancellation.TransactionHash), cancellationToken);
        return changed > 0;
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

    // Cancelling an escrow retires one freelancer from the project, not the
    // project itself. Their stages go back on the market, and the project
    // only reopens once nobody is left working on it.
    private async Task<bool> SyncCancelledAsync(
        Guid escrowId,
        Guid projectId,
        Guid freelancerId,
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

        // Release this freelancer's stages so the client can hire again.
        // Cancellation is only possible before work starts, so these are
        // all still Pending, but the reset keeps that an invariant of this
        // method rather than an assumption about the contract.
        await dbContext.Milestones
            .Where(item =>
                item.ProjectId == projectId &&
                item.FreelancerId == freelancerId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.FreelancerId,
                        (Guid?)null
                    )
                    .SetProperty(
                        item => item.Status,
                        MileStoneStatus.Pending
                    ),
                cancellationToken
            );

        // Their accepted proposal no longer holds anything. Leaving it
        // Accepted would tell the freelancer they are still hired.
        await dbContext.Proposals
            .Where(item =>
                item.ProjectId == projectId &&
                item.FreelancerId == freelancerId &&
                item.Status == ProposalStatus.Accepted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    ProposalStatus.Rejected
                ),
                cancellationToken
            );

        // Only the last freelancer leaving reopens the project; anyone else
        // still holding a stage keeps it in progress. The "nobody is left"
        // test is part of the same statement so it cannot be decided from a
        // stale read.
        await dbContext.Projects
            .Where(item =>
                item.Id == projectId &&
                item.Status == ProjectStatus.InProgress &&
                !item.Milestones.Any(
                    milestone => milestone.FreelancerId != null))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    ProjectStatus.Open
                ),
                cancellationToken
            );

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // Brings the backend's copy in line with what the contract says happened
    // after funding: cases opened or taken back, milestones the arbiter
    // settled, and an escrow that ended early. The chain is read, never
    // trusted from the caller, and every change is an atomic claim, so
    // running it twice or from two requests does the work once.
    private async Task<bool> SyncProgressAsync(
        Escrow escrow,
        OnChainEscrowSnapshot snapshot,
        int tokenDecimals,
        CancellationToken cancellationToken)
    {
        if (escrow.Status is not (EscrowStatus.AwaitingFunding or
            EscrowStatus.Funded))
        {
            return false;
        }

        var milestones = await dbContext.Milestones
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == escrow.ProjectId &&
                item.FreelancerId == escrow.FreelancerId)
            .OrderBy(item => item.SequenceNumber)
            .ToListAsync(cancellationToken);

        var changed = false;

        var released = Math.Min(
            snapshot.NextMilestoneIndex,
            milestones.Count
        );

        for (var index = 0; index < released; index++)
        {
            var milestone = milestones[index];

            if (milestone.Status is MileStoneStatus.Approved or
                MileStoneStatus.Settled)
            {
                continue;
            }

            var onChain = await deployer.GetMilestoneAsync(
                escrow.ContractAddress!,
                index,
                cancellationToken,
                escrow.PlatformFeeBasisPoints > 0
            );
            EscrowFeePolicy.ValidatePayment(onChain, escrow.PlatformFeeBasisPoints);

            // A release by the client is finished by the client's own
            // approval. Only a settlement is the arbiter's to report.
            if (!onChain.Released || !onChain.SettledByCase)
            {
                continue;
            }

            changed |= await ApplySettlementAsync(
                escrow,
                milestone,
                onChain,
                tokenDecimals,
                snapshot.State == OnChainEscrowState.Terminated &&
                    index == snapshot.NextMilestoneIndex - 1,
                cancellationToken
            );
        }

        if (snapshot.State == OnChainEscrowState.Terminated)
        {
            changed |= await ApplyTerminationAsync(
                escrow,
                milestones,
                snapshot.NextMilestoneIndex,
                cancellationToken
            );
        }

        changed |= await ReconcileOpenCaseAsync(
            escrow,
            milestones,
            snapshot,
            cancellationToken
        );

        if (snapshot.State == OnChainEscrowState.Completed)
        {
            changed |= await ApplyCompletionAsync(
                escrow,
                cancellationToken
            );
        }

        return changed;
    }

    private async Task<bool> ApplySettlementAsync(
        Escrow escrow,
        MileStone milestone,
        OnChainMilestone onChain,
        int tokenDecimals,
        bool escrowEnded,
        CancellationToken cancellationToken)
    {
        var amount = TokenAmountConverter.FromBaseUnits(
            onChain.Amount,
            tokenDecimals
        );

        var fee = TokenAmountConverter.FromBaseUnits(onChain.PlatformFee, tokenDecimals);
        var paid = TokenAmountConverter.FromBaseUnits(
            onChain.Paid,
            tokenDecimals
        );

        if (amount != milestone.Amount || paid > amount)
        {
            throw new InvalidOperationException(
                $"On-chain milestone for {milestone.Id} " +
                $"(amount {amount}, paid {paid}) does not match the " +
                $"backend amount {milestone.Amount}."
            );
        }

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.Milestones
            .Where(item =>
                item.Id == milestone.Id &&
                item.Status != MileStoneStatus.Approved &&
                item.Status != MileStoneStatus.Settled)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        MileStoneStatus.Settled)
                    .SetProperty(
                        item => item.PaidAmount,
                        (decimal?)paid)
                    .SetProperty(item => item.PlatformFeePaid, fee),
                cancellationToken
            );

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await dbContext.Escrows
            .Where(item => item.Id == escrow.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.ReleasedAmount,
                        item => item.ReleasedAmount + paid)
                    .SetProperty(item => item.PlatformFeesPaid, item => item.PlatformFeesPaid + fee)
                    .SetProperty(
                        item => item.UpdatedAt,
                        now),
                cancellationToken
            );

        var closedCases = await dbContext.EscrowCases
            .Where(item =>
                item.EscrowId == escrow.Id &&
                item.MilestoneId == milestone.Id &&
                item.Status == EscrowCaseStatus.Open)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowCaseStatus.Settled)
                    .SetProperty(
                        item => item.ClosedAt,
                        (DateTimeOffset?)now)
                    .SetProperty(
                        item => item.PaidToFreelancer,
                        (decimal?)paid)
                    .SetProperty(item => item.PlatformFeePaid, fee)
                    .SetProperty(
                        item => item.ReturnedToClient,
                        (decimal?)(amount - paid))
                    .SetProperty(
                        item => item.EscrowEnded,
                        escrowEnded),
                cancellationToken
            );

        if (closedCases == 0)
        {
            // The case was settled before this backend ever saw it open.
            // The kind is the only thing that cannot be recovered, so it is
            // read from the outcome.
            dbContext.EscrowCases.Add(new EscrowCase
            {
                EscrowId = escrow.Id,
                MilestoneId = milestone.Id,
                Kind = paid > 0m
                    ? EscrowCaseKind.PaymentRequest
                    : EscrowCaseKind.RefundRequest,
                OpenedAt = now,
                Status = EscrowCaseStatus.Settled,
                ClosedAt = now,
                PaidToFreelancer = paid,
                ReturnedToClient = amount - paid,
                EscrowEnded = escrowEnded
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await SettleDisputesAsync(
            [milestone.Id],
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        dbContext.ChangeTracker.Clear();

        return true;
    }

    // The escrow ended early. What was settled or approved stays; every
    // stage still ahead goes back on the marketplace for another freelancer.
    private async Task<bool> ApplyTerminationAsync(
        Escrow escrow,
        IReadOnlyList<MileStone> milestones,
        int nextMilestoneIndex,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.Escrows
            .Where(item =>
                item.Id == escrow.Id &&
                (item.Status == EscrowStatus.Funded ||
                 item.Status == EscrowStatus.AwaitingFunding))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.Terminated)
                    .SetProperty(
                        item => item.UpdatedAt,
                        now),
                cancellationToken
            );

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        // Stages the client already released on-chain keep their owner; the
        // client's approval will close them. Everything from the contract's
        // next position onwards was never paid.
        var ahead = milestones
            .Skip(Math.Min(nextMilestoneIndex, milestones.Count))
            .Select(item => item.Id)
            .ToList();

        await SettleDisputesAsync(ahead, cancellationToken);

        await dbContext.Milestones
            .Where(item =>
                ahead.Contains(item.Id) &&
                item.Status != MileStoneStatus.Approved &&
                item.Status != MileStoneStatus.Settled)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.FreelancerId,
                        (Guid?)null)
                    .SetProperty(
                        item => item.Status,
                        MileStoneStatus.Pending)
                    .SetProperty(
                        item => item.PaidAmount,
                        (decimal?)null),
                cancellationToken
            );

        await dbContext.Proposals
            .Where(item =>
                item.ProjectId == escrow.ProjectId &&
                item.FreelancerId == escrow.FreelancerId &&
                item.Status == ProposalStatus.Accepted)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    ProposalStatus.Rejected),
                cancellationToken
            );

        await dbContext.EscrowCases
            .Where(item =>
                item.EscrowId == escrow.Id &&
                item.Status == EscrowCaseStatus.Open)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowCaseStatus.Withdrawn)
                    .SetProperty(
                        item => item.ClosedAt,
                        (DateTimeOffset?)now),
                cancellationToken
            );

        // The project is finished only when no stage is left unfinished.
        await CompleteProjectIfDoneAsync(
            escrow.ProjectId,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        dbContext.ChangeTracker.Clear();

        return true;
    }

    private async Task<bool> ReconcileOpenCaseAsync(
        Escrow escrow,
        IReadOnlyList<MileStone> milestones,
        OnChainEscrowSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        var openRow = await dbContext.EscrowCases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.EscrowId == escrow.Id &&
                    item.Status == EscrowCaseStatus.Open,
                cancellationToken
            );

        var onChainOpen =
            snapshot.State == OnChainEscrowState.Funded &&
            snapshot.OpenCaseKind != OnChainCaseKind.None &&
            snapshot.OpenCaseOpenedAt is not null &&
            snapshot.NextMilestoneIndex < milestones.Count;

        var kind = snapshot.OpenCaseKind == OnChainCaseKind.RefundRequest
            ? EscrowCaseKind.RefundRequest
            : EscrowCaseKind.PaymentRequest;

        var milestoneId = onChainOpen
            ? milestones[snapshot.NextMilestoneIndex].Id
            : Guid.Empty;

        if (openRow is not null &&
            onChainOpen &&
            openRow.Kind == kind &&
            openRow.MilestoneId == milestoneId &&
            openRow.OpenedAt.ToUnixTimeSeconds() ==
                snapshot.OpenCaseOpenedAt!.Value.ToUnixTimeSeconds())
        {
            return false;
        }

        var changed = false;

        // Taken back, or closed by a release or a resignation.
        if (openRow is not null)
        {
            changed = await dbContext.EscrowCases
                .Where(item =>
                    item.Id == openRow.Id &&
                    item.Status == EscrowCaseStatus.Open)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            item => item.Status,
                            EscrowCaseStatus.Withdrawn)
                        .SetProperty(
                            item => item.ClosedAt,
                            (DateTimeOffset?)now),
                    cancellationToken
                ) > 0;
        }

        if (!onChainOpen)
        {
            return changed;
        }

        dbContext.EscrowCases.Add(new EscrowCase
        {
            EscrowId = escrow.Id,
            MilestoneId = milestoneId,
            Kind = kind,
            OpenedAt = snapshot.OpenCaseOpenedAt!.Value
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            // Another request recorded the same case first.
            dbContext.ChangeTracker.Clear();
            return changed;
        }

        dbContext.ChangeTracker.Clear();

        return true;
    }

    private async Task<bool> ApplyCompletionAsync(
        Escrow escrow,
        CancellationToken cancellationToken)
    {
        // The escrow is finished in the backend's eyes only when every stage
        // it pays for is closed. A stage the client released but has not yet
        // approved is closed by that approval, which also completes this.
        var anyOpen = await dbContext.Milestones
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProjectId == escrow.ProjectId &&
                    item.FreelancerId == escrow.FreelancerId &&
                    item.Status != MileStoneStatus.Approved &&
                    item.Status != MileStoneStatus.Settled,
                cancellationToken
            );

        if (anyOpen)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        var claimed = await dbContext.Escrows
            .Where(item =>
                item.Id == escrow.Id &&
                item.Status == EscrowStatus.Funded)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        EscrowStatus.Completed)
                    .SetProperty(
                        item => item.CompletedAt,
                        (DateTimeOffset?)now)
                    .SetProperty(
                        item => item.UpdatedAt,
                        now),
                cancellationToken
            );

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await CompleteProjectIfDoneAsync(
            escrow.ProjectId,
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        return true;
    }

    private async Task CompleteProjectIfDoneAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        await dbContext.Projects
            .Where(item =>
                item.Id == projectId &&
                item.Status == ProjectStatus.InProgress &&
                !item.Milestones.Any(milestone =>
                    milestone.Status != MileStoneStatus.Approved &&
                    milestone.Status != MileStoneStatus.Settled))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    ProjectStatus.Completed),
                cancellationToken
            );
    }

    // A dispute is advice about a milestone that was still waiting on the
    // client. Once the milestone is settled or sent back it has no purpose.
    private async Task SettleDisputesAsync(
        IReadOnlyCollection<Guid> milestoneIds,
        CancellationToken cancellationToken)
    {
        await dbContext.Disputes
            .Where(item =>
                milestoneIds.Contains(item.MilestoneId) &&
                (item.Status == DisputeStatus.Open ||
                 item.Status == DisputeStatus.Ruled))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.Status,
                    DisputeStatus.Settled),
                cancellationToken
            );
    }
}
