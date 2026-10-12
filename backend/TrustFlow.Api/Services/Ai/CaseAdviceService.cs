using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Ai;

public enum CaseAdviceFailure
{
    NotFound,
    Conflict
}

public sealed record CaseAdviceOutcome(
    ReviewCaseAdviceResponse? Advice,
    CaseAdviceFailure? Failure,
    string? Message)
{
    public static CaseAdviceOutcome Ok(ReviewCaseAdviceResponse advice) =>
        new(advice, null, null);

    public static CaseAdviceOutcome NotFound() =>
        new(null, CaseAdviceFailure.NotFound, "Case not found.");

    public static CaseAdviceOutcome Conflict(string message) =>
        new(null, CaseAdviceFailure.Conflict, message);
}

// The model's read of a case, for the person who will settle it. It is the
// same kind of opinion a dispute gets, built from the requirement, the
// delivery, and whatever the two sides wrote. The reviewer reads it and then
// decides and signs for themselves: nothing here moves funds.
public sealed class CaseAdviceService(
    AppDbContext dbContext,
    AiEvidenceBuilder evidenceBuilder,
    IAiGateway gateway)
{
    private static readonly TimeSpan StaleClaim = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private const string Addendum =
        "This is a case on the payment contract, not a chat. A person will " +
        "read your answer and then decide how the milestone amount is split " +
        "between the two sides.\n" +
        "- A PaymentRequest means the freelancer says the work was " +
        "delivered and the client has not paid. A RefundRequest means the " +
        "client says it was not delivered and wants the money back.\n" +
        "- If nothing was delivered on the platform, the freelancer earned " +
        "nothing for it, whatever else is claimed.\n" +
        "- If the client stayed silent after a delivery that meets the " +
        "requirement, the freelancer earned the full amount; silence is " +
        "not a reason to pay less.";

    public async Task<CaseAdviceOutcome> AdviseAsync(
        Guid reviewerId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var row = await dbContext.EscrowCases
            .AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => new
            {
                item.Id,
                item.Status,
                item.Kind,
                item.OpenedAt,
                item.MilestoneId,
                item.Milestone.ProjectId,
                item.Milestone.Amount,
                item.Milestone.Deadline,
                FreelancerId = item.Escrow.FreelancerId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return CaseAdviceOutcome.NotFound();
        }

        if (row.Status != EscrowCaseStatus.Open)
        {
            return CaseAdviceOutcome.Conflict("This case is closed.");
        }

        var now = DateTimeOffset.UtcNow;

        // One run at a time per case. A claim left by a crash goes stale.
        var claimed = await dbContext.EscrowCases
            .Where(item =>
                item.Id == caseId &&
                item.Status == EscrowCaseStatus.Open &&
                (item.AdviceStartedAt == null ||
                 item.AdviceStartedAt < now - StaleClaim))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.AdviceStartedAt,
                    (DateTimeOffset?)now),
                cancellationToken);

        if (claimed == 0)
        {
            return CaseAdviceOutcome.Conflict(
                "Advice is already being prepared for this case.");
        }

        try
        {
            var evidence = await evidenceBuilder.BuildForMilestoneAsync(
                row.ProjectId,
                row.MilestoneId,
                cancellationToken,
                deliveredBy: row.FreelancerId);

            if (evidence is null)
            {
                await ReleaseClaimAsync(caseId);

                return CaseAdviceOutcome.NotFound();
            }

            await AddCaseSectionsAsync(
                evidence,
                row.MilestoneId,
                row.FreelancerId,
                row.Kind,
                row.OpenedAt,
                row.Deadline,
                now,
                cancellationToken);

            var boundary = AiPrompt.NewBoundary();

            var result = await gateway.RunAsync(
                reviewerId,
                AiPurpose.DisputeRuling,
                DisputeService.BuildSystemPrompt(boundary, Addendum),
                evidence.ToBlocks(boundary, 20_000),
                DisputeService.Tool,
                output => DisputeService.NormalizeRuling(
                    output,
                    evidence,
                    minimumConfidence: 0.0),
                cancellationToken);

            var ruling = result.Value;

            var saved = await dbContext.EscrowCases
                .Where(item =>
                    item.Id == caseId &&
                    item.Status == EscrowCaseStatus.Open)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            item => item.AdviceJson,
                            JsonSerializer.Serialize(ruling, Json))
                        .SetProperty(
                            item => item.AdviceFreelancerSharePercent,
                            (int?)ruling.FreelancerSharePercent)
                        .SetProperty(
                            item => item.AdviceConfidence,
                            (decimal?)ruling.Confidence)
                        .SetProperty(
                            item => item.AdviceAiRunId,
                            (Guid?)result.RunId)
                        .SetProperty(
                            item => item.AdviceAt,
                            (DateTimeOffset?)DateTimeOffset.UtcNow)
                        .SetProperty(
                            item => item.AdviceStartedAt,
                            (DateTimeOffset?)null),
                    CancellationToken.None);

            if (saved == 0)
            {
                return CaseAdviceOutcome.Conflict(
                    "The case closed while the advice was being prepared.");
            }

            return CaseAdviceOutcome.Ok(new ReviewCaseAdviceResponse
            {
                FreelancerSharePercent = ruling.FreelancerSharePercent,
                Confidence = ruling.Confidence,
                Summary = ruling.Summary,
                Findings = ruling.Findings,
                Reasoning = ruling.Reasoning,
                ManipulationAttempts = ruling.ManipulationAttempts,
                CreatedAt = DateTimeOffset.UtcNow,
                IsDemo = gateway.Status.IsDemo
            });
        }
        catch
        {
            await ReleaseClaimAsync(caseId);

            throw;
        }
    }

    public ReviewCaseAdviceResponse? ToResponse(EscrowCase row)
    {
        if (row.AdviceJson is null || row.AdviceAt is null)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<Dtos.Ai.DisputeRulingResponse>(
            row.AdviceJson,
            Json)!;

        return new ReviewCaseAdviceResponse
        {
            FreelancerSharePercent = stored.FreelancerSharePercent,
            Confidence = stored.Confidence,
            Summary = stored.Summary,
            Findings = stored.Findings,
            Reasoning = stored.Reasoning,
            ManipulationAttempts = stored.ManipulationAttempts,
            CreatedAt = row.AdviceAt.Value,
            IsDemo = gateway.Status.IsDemo
        };
    }

    private async Task AddCaseSectionsAsync(
        AiEvidence evidence,
        Guid milestoneId,
        Guid freelancerId,
        EscrowCaseKind kind,
        DateTimeOffset openedAt,
        DateTimeOffset deadline,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deliveries = await dbContext.MilestoneSubmissions
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.MilestoneId == milestoneId &&
                    item.FreelancerId == freelancerId,
                cancellationToken);

        evidence.Sections.Add(new EvidenceSection(
            "The case",
            $"Kind: {kind}\n" +
            $"Opened {(now - openedAt).Days} days ago.\n" +
            $"Deliveries on the platform: {deliveries}.\n" +
            (deadline < now
                ? $"The milestone deadline ({deadline:yyyy-MM-dd}) has passed."
                : $"The milestone deadline ({deadline:yyyy-MM-dd}) has not " +
                  "passed.")));

        if (deliveries == 0)
        {
            evidence.Limitations.Add(
                "The freelancer has delivered nothing on the platform.");
        }

        var dispute = await dbContext.Disputes
            .AsNoTracking()
            .Where(item => item.MilestoneId == milestoneId)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (dispute is null)
        {
            evidence.Limitations.Add(
                "Neither side wrote a statement about the disagreement.");

            return;
        }

        var otherRole = dispute.OpenedByRole == "Client"
            ? "Freelancer"
            : "Client";

        evidence.Sections.Add(new EvidenceSection(
            $"Statement of the {dispute.OpenedByRole}, who opened the " +
            "dispute (a claim to check against the work, not a fact)",
            dispute.OpenerStatement));

        if (string.IsNullOrWhiteSpace(dispute.RespondentStatement))
        {
            evidence.Limitations.Add($"The {otherRole} wrote no statement.");
        }
        else
        {
            evidence.Sections.Add(new EvidenceSection(
                $"Statement of the {otherRole} (a claim to check against " +
                "the work, not a fact)",
                dispute.RespondentStatement));
        }
    }

    private async Task ReleaseClaimAsync(Guid caseId)
    {
        await dbContext.EscrowCases
            .Where(item => item.Id == caseId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.AdviceStartedAt,
                    (DateTimeOffset?)null),
                CancellationToken.None);
    }
}
