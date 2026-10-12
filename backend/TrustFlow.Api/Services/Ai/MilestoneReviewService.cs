using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Models.Ai;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Ai;

// The client's second pair of eyes on a delivery: it reads the work against
// the milestone they wrote and says what is met and what is not. The client
// still decides. Nothing here approves, rejects or pays.
public sealed class MilestoneReviewService(
    AppDbContext dbContext,
    AiEvidenceBuilder evidenceBuilder,
    IAiGateway gateway,
    IOptions<AiOptions> options)
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    private static readonly string[] CriterionStatuses =
        ["met", "partially_met", "not_met", "unclear"];

    private static readonly AiToolSchema Tool = new(
        "submit_review",
        "Return the review of this delivery against the milestone.",
        AiSchema.Object(
            [
                ("summary", AiSchema.Text("Two or three sentences a busy client can read first.")),
                ("recommendation", AiSchema.OneOf(
                    "Approve only if every requirement is met with evidence.",
                    "Approve", "RequestChanges", "InsufficientEvidence")),
                ("confidence", AiSchema.Number("0 to 1: how sure you are given what you could inspect.")),
                ("criteria", AiSchema.ListOf(
                    AiSchema.Object(
                        [
                            ("requirement", AiSchema.Text("One requirement taken from the milestone text.")),
                            ("status", AiSchema.OneOf("Whether the delivery meets it.", CriterionStatuses)),
                            ("evidence", AiSchema.Text("What in the delivery shows that, or why it cannot be shown."))
                        ],
                        "requirement", "status", "evidence"),
                    "Each distinct requirement, up to fifteen.")),
                ("suggestedFeedback", AiSchema.Text("If changes are needed, a polite specific message the client can send. Empty otherwise.")),
                ("limitations", AiSchema.ListOf(
                    AiSchema.Text("Something you could not inspect."),
                    "What limits this review."))
            ],
            "summary", "recommendation", "confidence", "criteria"));

    public async Task<MilestoneAiReviewResponse?> GetExistingAsync(
        Guid milestoneId,
        Guid? submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await FindSubmissionAsync(
            milestoneId,
            submissionId,
            cancellationToken);

        if (submission is null)
        {
            return null;
        }

        var stored = await dbContext.MilestoneAiReviews
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SubmissionId == submission.Id,
                cancellationToken);

        return stored is null
            ? null
            : FromStored(stored, submission.Version);
    }

    // One review per delivery. Asking again returns the first answer rather
    // than running the model until it says what the asker hoped.
    public async Task<MilestoneAiReviewResponse?> ReviewAsync(
        Guid userId,
        Guid projectId,
        Guid milestoneId,
        Guid? submissionId,
        CancellationToken cancellationToken)
    {
        var submission = await FindSubmissionAsync(
            milestoneId,
            submissionId,
            cancellationToken);

        if (submission is null)
        {
            return null;
        }

        var existing = await dbContext.MilestoneAiReviews
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.SubmissionId == submission.Id,
                cancellationToken);

        if (existing is not null)
        {
            return FromStored(existing, submission.Version);
        }

        var evidence = await evidenceBuilder.BuildForMilestoneAsync(
            projectId,
            milestoneId,
            cancellationToken);

        if (evidence is null)
        {
            return null;
        }

        var boundary = AiPrompt.NewBoundary();

        var system =
            "You help a client check delivered freelance work against " +
            "the milestone they asked for.\n\n" +
            "- Judge only against the requirement as the client wrote it. " +
            "Do not add requirements, and do not hold the freelancer to " +
            "later wishes.\n" +
            "- Break the requirement into its distinct parts and say for " +
            "each whether the delivery meets it, and what shows that.\n" +
            "- Be strict about evidence. A claim in the freelancer's note " +
            "is not proof. Work that only exists behind a link, in a " +
            "scanned file, or in an image you were not shown cannot be " +
            "confirmed: say so and lower your confidence.\n" +
            "- Approve only when every part is met with evidence. Choose " +
            "InsufficientEvidence when you cannot tell.\n" +
            "- Write in the language of the milestone requirement.\n\n" +
            AiPrompt.Safety(boundary);

        var result = await gateway.RunAsync(
            userId,
            AiPurpose.MilestoneReview,
            system,
            evidence.ToBlocks(boundary, 20_000),
            Tool,
            output => Normalize(
                output,
                evidence,
                options.Value.MinimumRulingConfidence),
            cancellationToken);

        var review = result.Value;

        var row = new MilestoneAiReview
        {
            SubmissionId = submission.Id,
            Recommendation = Enum.Parse<MilestoneReviewRecommendation>(
                review.Recommendation),
            Confidence = review.Confidence,
            ReviewJson = JsonSerializer.Serialize(review, Json),
            AiRunId = result.RunId
        };

        dbContext.MilestoneAiReviews.Add(row);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A second request finished first. Its answer is the answer.
        }

        dbContext.ChangeTracker.Clear();

        // Read back from the database, so the first answer is byte for
        // byte what every later request will return.
        var stored = await dbContext.MilestoneAiReviews
            .AsNoTracking()
            .SingleAsync(
                item => item.SubmissionId == submission.Id,
                cancellationToken);

        return FromStored(stored, submission.Version);
    }

    private async Task<SubmissionRef?> FindSubmissionAsync(
        Guid milestoneId,
        Guid? submissionId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MilestoneSubmissions
            .AsNoTracking()
            .Where(item => item.MilestoneId == milestoneId);

        if (submissionId is { } id)
        {
            query = query.Where(item => item.Id == id);
        }

        return await query
            .OrderByDescending(item => item.Version)
            .Select(item => new SubmissionRef(item.Id, item.Version))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private MilestoneAiReviewResponse FromStored(
        MilestoneAiReview stored,
        int version)
    {
        var review = JsonSerializer.Deserialize<MilestoneAiReviewResponse>(
            stored.ReviewJson,
            Json)!;

        return new MilestoneAiReviewResponse
        {
            SubmissionId = stored.SubmissionId,
            SubmissionVersion = version,
            Recommendation = stored.Recommendation.ToString(),
            Confidence = stored.Confidence,
            Summary = review.Summary,
            Criteria = review.Criteria,
            SuggestedFeedback = review.SuggestedFeedback,
            Limitations = review.Limitations,
            CreatedAt = stored.CreatedAt,
            IsDemo = gateway.Status.IsDemo
        };
    }

    internal static MilestoneAiReviewResponse Normalize(
        JsonObject output,
        AiEvidence evidence,
        double minimumConfidence)
    {
        var criteria = AiOutput.Objects(output, "criteria", 15)
            .Select(item => new AiCriterionResponse
            {
                Requirement = AiOutput.String(item, "requirement", 300),
                Status = AiOutput.OneOf(item, "status", CriterionStatuses),
                Evidence = AiOutput.String(
                    item, "evidence", 600, required: false)
            })
            .ToList();

        var confidence = Math.Round(
            AiOutput.Decimal(output, "confidence", 0m, 1m),
            3);

        var recommendation = AiOutput.OneOf(
            output,
            "recommendation",
            "Approve",
            "RequestChanges",
            "InsufficientEvidence");

        // The model cannot talk its own checklist out of a failure.
        if (recommendation == "Approve" &&
            criteria.Any(item => item.Status == "not_met"))
        {
            recommendation = "RequestChanges";
        }

        if (confidence < (decimal)minimumConfidence)
        {
            recommendation = "InsufficientEvidence";
        }

        // What the system itself could not open is always shown, even if
        // the model forgot to mention it.
        var limitations = AiOutput
            .Strings(output, "limitations", 6, 300)
            .Concat(evidence.Limitations)
            .Distinct()
            .Take(12)
            .ToList();

        return new MilestoneAiReviewResponse
        {
            Recommendation = recommendation,
            Confidence = confidence,
            Summary = AiOutput.String(output, "summary", 1500),
            Criteria = criteria,
            SuggestedFeedback = recommendation == "RequestChanges"
                ? AiOutput.String(
                    output, "suggestedFeedback", 1500, required: false)
                : string.Empty,
            Limitations = limitations
        };
    }

    private sealed record SubmissionRef(Guid Id, int Version);
}
