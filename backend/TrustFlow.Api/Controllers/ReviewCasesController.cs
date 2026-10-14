using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Ai;

namespace TrustFlow.Api.Controllers;

// The reviewer's desk. The reviewer is a person with the Admin role who also
// holds the arbiter wallet. They read the evidence here, optionally ask the
// model for advice, and sign the settlement from their own wallet on the
// contract. This controller never signs anything and has no way to settle:
// it only shows what the chain allows and records what the chain did.
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/review/cases")]
public sealed class ReviewCasesController(
    AppDbContext dbContext,
    IEscrowChainDeployer chainDeployer,
    IEscrowChainSyncOrchestrator chainSync,
    CaseAdviceService adviceService,
    IOptions<AiOptions> aiOptions)
    : ControllerBase
{
    private const int NoteMaxLength = 2000;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var closed = string.Equals(
            status,
            "closed",
            StringComparison.OrdinalIgnoreCase);

        var query = dbContext.EscrowCases.AsNoTracking();

        query = closed
            ? query.Where(item => item.Status != EscrowCaseStatus.Open)
            : query.Where(item => item.Status == EscrowCaseStatus.Open);

        var rows = await query
            .OrderBy(item => item.Status == EscrowCaseStatus.Open
                ? item.OpenedAt
                : DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.ClosedAt)
            .Take(100)
            .Select(item => new
            {
                Case = item,
                ProjectTitle = item.Milestone.Project.Title,
                MilestoneTitle = item.Milestone.Title,
                MilestoneAmount = item.Milestone.Amount,
                ProjectId = item.Milestone.ProjectId
            })
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return Ok(rows.Select(row => ToSummary(
            row.Case,
            row.ProjectId,
            row.ProjectTitle,
            row.MilestoneTitle,
            row.MilestoneAmount,
            now)));
    }

    [HttpGet("{caseId:guid}")]
    public async Task<IActionResult> Get(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var detail = await BuildDetailAsync(caseId, cancellationToken);

        return detail is null
            ? NotFound(new { message = "Case not found." })
            : Ok(detail);
    }

    // The chain is the truth. Reading it here is free and changes only what
    // the contract already did, so the reviewer can refresh after signing.
    [HttpPost("{caseId:guid}/sync")]
    public async Task<IActionResult> Sync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var escrowId = await dbContext.EscrowCases
            .AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => (Guid?)item.EscrowId)
            .SingleOrDefaultAsync(cancellationToken);

        if (escrowId is null)
        {
            return NotFound(new { message = "Case not found." });
        }

        await chainSync.TrySyncAsync(escrowId.Value, cancellationToken);

        dbContext.ChangeTracker.Clear();

        var detail = await BuildDetailAsync(caseId, cancellationToken);

        return detail is null
            ? NotFound(new { message = "Case not found." })
            : Ok(detail);
    }

    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [HttpPost("{caseId:guid}/advice")]
    public async Task<IActionResult> Advise(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var reviewerId))
        {
            return Unauthorized();
        }

        var outcome = await adviceService.AdviseAsync(
            reviewerId,
            caseId,
            cancellationToken);

        if (outcome.Failure == CaseAdviceFailure.NotFound)
        {
            return NotFound(new { message = outcome.Message });
        }

        if (outcome.Failure == CaseAdviceFailure.Conflict)
        {
            return Conflict(new { message = outcome.Message });
        }

        dbContext.ChangeTracker.Clear();

        var detail = await BuildDetailAsync(caseId, cancellationToken);

        return detail is null
            ? NotFound(new { message = "Case not found." })
            : Ok(detail);
    }

    // The reasons the two sides will read next to the outcome.
    [HttpPut("{caseId:guid}/note")]
    public async Task<IActionResult> SaveNote(
        Guid caseId,
        SaveReviewNoteRequest request,
        CancellationToken cancellationToken)
    {
        var note = (request.Note ?? string.Empty).Trim();

        if (note.Length == 0 || note.Length > NoteMaxLength)
        {
            return BadRequest(new
            {
                message =
                    $"The note must be between 1 and {NoteMaxLength} " +
                    "characters."
            });
        }

        var now = DateTimeOffset.UtcNow;

        // A note can be written for an open case and until it is read by
        // the sides; the settled case keeps whatever was written last.
        var written = await dbContext.EscrowCases
            .Where(item => item.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.ReviewNote,
                        note)
                    .SetProperty(
                        item => item.ReviewNoteAt,
                        (DateTimeOffset?)now),
                cancellationToken);

        if (written == 0)
        {
            return NotFound(new { message = "Case not found." });
        }

        var detail = await BuildDetailAsync(caseId, cancellationToken);

        return detail is null
            ? NotFound(new { message = "Case not found." })
            : Ok(detail);
    }

    [HttpGet("{caseId:guid}/files/{attachmentId:guid}")]
    public async Task<IActionResult> Download(
        Guid caseId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        // Only files delivered by the freelancer this case is about, for
        // this case's milestone.
        var attachment = await (
            from reviewCase in dbContext.EscrowCases.AsNoTracking()
            join submission in dbContext.MilestoneSubmissions
                on reviewCase.MilestoneId equals submission.MilestoneId
            join file in dbContext.MilestoneSubmissionAttachments
                on submission.Id equals file.SubmissionId
            where reviewCase.Id == caseId &&
                  submission.FreelancerId == reviewCase.Escrow.FreelancerId &&
                  file.Id == attachmentId
            select new
            {
                file.Content,
                file.ContentType,
                file.OriginalFileName
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (attachment is null)
        {
            return NotFound(new { message = "Delivery file not found." });
        }

        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox";

        return File(
            attachment.Content,
            attachment.ContentType,
            attachment.OriginalFileName,
            enableRangeProcessing: false);
    }

    private async Task<ReviewCaseDetailResponse?> BuildDetailAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.EscrowCases
            .AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => new
            {
                Case = item,
                ProjectId = item.Milestone.ProjectId,
                ProjectTitle = item.Milestone.Project.Title,
                ProjectDescription = item.Milestone.Project.Description,
                MilestoneTitle = item.Milestone.Title,
                MilestoneDescription = item.Milestone.Description,
                MilestoneAmount = item.Milestone.Amount,
                MilestoneSequence = item.Milestone.SequenceNumber,
                MilestoneDeadline = item.Milestone.Deadline,
                MilestoneStatus = item.Milestone.Status,
                item.Escrow.FreelancerId,
                item.Escrow.ChainId,
                item.Escrow.ContractAddress,
                item.Escrow.ArbiterWalletAddress,
                item.Escrow.PlatformFeeBasisPoints,
                item.Escrow.TokenAddress
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var onChainIndex = await dbContext.Milestones
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.ProjectId == row.ProjectId &&
                    item.FreelancerId == row.FreelancerId &&
                    item.SequenceNumber < row.MilestoneSequence,
                cancellationToken);

        var deliveries = await dbContext.MilestoneSubmissions
            .AsNoTracking()
            .Where(item =>
                item.MilestoneId == row.Case.MilestoneId &&
                item.FreelancerId == row.FreelancerId)
            .Include(item => item.Attachments)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .Where(item => item.MilestoneId == row.Case.MilestoneId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var tokenDecimals = await chainDeployer.GetTokenDecimalsAsync(
            row.TokenAddress,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return new ReviewCaseDetailResponse
        {
            Case = ToSummary(
                row.Case,
                row.ProjectId,
                row.ProjectTitle,
                row.MilestoneTitle,
                row.MilestoneAmount,
                now),
            ProjectDescription = row.ProjectDescription,
            MilestoneDescription = row.MilestoneDescription,
            MilestoneSequence = row.MilestoneSequence,
            MilestoneDeadline = row.MilestoneDeadline,
            DeadlinePassed = row.MilestoneDeadline < now,
            MilestoneStatus = row.MilestoneStatus.ToString(),
            ChainId = row.ChainId,
            ContractAddress = row.ContractAddress,
            ArbiterWalletAddress = row.ArbiterWalletAddress,
            PlatformFeeBasisPoints = row.PlatformFeeBasisPoints,
            TokenAddress = row.TokenAddress,
            TokenDecimals = tokenDecimals,
            OnChainMilestoneIndex = onChainIndex,
            Deliveries = deliveries.Select(ToDelivery).ToList(),
            Dispute = dispute is null
                ? null
                : ToStatements(dispute),
            Advice = adviceService.ToResponse(row.Case),
            ReviewNote = row.Case.ReviewNote,
            AiEnabled = aiOptions.Value.Enabled
        };
    }

    private static ReviewCaseSummaryResponse ToSummary(
        EscrowCase item,
        Guid projectId,
        string projectTitle,
        string milestoneTitle,
        decimal milestoneAmount,
        DateTimeOffset now)
    {
        var settleableAt = item.OpenedAt + EscrowCaseRules.ResponseWindow;

        return new ReviewCaseSummaryResponse
        {
            Id = item.Id,
            ProjectId = projectId,
            ProjectTitle = projectTitle,
            MilestoneTitle = milestoneTitle,
            MilestoneAmount = milestoneAmount,
            Kind = item.Kind.ToString(),
            OpenedByRole = EscrowCasesController.OpenedByRole(item.Kind),
            Status = item.Status.ToString(),
            OpenedAt = item.OpenedAt,
            SettleableAt = settleableAt,
            IsReady = item.Status == EscrowCaseStatus.Open &&
                      now >= settleableAt,
            HasAdvice = item.AdviceJson is not null,
            EscrowEnded = item.EscrowEnded,
            PaidToFreelancer = item.PaidToFreelancer,
            PlatformFeePaid = item.PlatformFeePaid,
            FreelancerNetPaid = item.PaidToFreelancer - item.PlatformFeePaid,
            ReturnedToClient = item.ReturnedToClient,
            ClosedAt = item.ClosedAt
        };
    }

    private static MilestoneSubmissionResponse ToDelivery(
        MilestoneSubmission submission) => new()
    {
        Id = submission.Id,
        MilestoneId = submission.MilestoneId,
        Version = submission.Version,
        Note = submission.Note,
        PreviewLink = submission.PreviewLink,
        SourceFilesLink = submission.SourceFilesLink,
        ClientFeedback = submission.ClientFeedback,
        SubmittedAt = submission.SubmittedAt,
        ReviewedAt = submission.ReviewedAt,
        Attachments = submission.Attachments
            .Select(file => new MilestoneSubmissionAttachmentResponse
            {
                Id = file.Id,
                FileName = file.OriginalFileName,
                ContentType = file.ContentType,
                FileSize = file.FileSize,
                Sha256 = file.Sha256
            })
            .ToList()
    };

    private static ReviewCaseStatementsResponse ToStatements(
        Models.Ai.Dispute dispute)
    {
        string? summary = null;

        if (dispute.RulingJson is not null)
        {
            using var document = JsonDocument.Parse(dispute.RulingJson);

            if (document.RootElement.TryGetProperty(
                    "summary",
                    out var element) &&
                element.ValueKind == JsonValueKind.String)
            {
                summary = element.GetString();
            }
        }

        return new ReviewCaseStatementsResponse
        {
            OpenedByRole = dispute.OpenedByRole,
            OpenerStatement = dispute.OpenerStatement,
            RespondentStatement = dispute.RespondentStatement,
            Status = dispute.Status.ToString(),
            RulingFreelancerSharePercent = dispute.FreelancerSharePercent,
            RulingSummary = summary
        };
    }
}
