using System.Text.Json.Nodes;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Ai;

// Answers with fixed output so the whole flow can be built, shown and tested
// without paying for a model. Nothing here reads the user's input, and every
// text it returns says so, so a demo cannot be mistaken for real analysis.
public sealed class MockAiModel : IAiModel
{
    public const string DemoNotice =
        "DEMO MODE: this is placeholder text, not a real analysis.";

    public Task<AiModelResponse> CompleteAsync(
        AiModelRequest request,
        CancellationToken cancellationToken)
    {
        JsonObject output = request.Purpose switch
        {
            AiPurpose.ProjectDraft => ProjectDraft(),
            AiPurpose.MilestoneReview => MilestoneReview(),
            AiPurpose.FreelancerProfileCoach => ProfileCoach(),
            AiPurpose.FreelancerResumeDraft => ResumeDraft(),
            AiPurpose.DisputeRuling => DisputeRuling(),
            _ => throw new AiModelException(
                "unsupported",
                "The demo model does not support this request.")
        };

        return Task.FromResult(new AiModelResponse(output, 0, 0));
    }

    private static JsonObject ProjectDraft() => new()
    {
        ["title"] = "Demo project",
        ["description"] =
            $"{DemoNotice} Describe what you need built, who it is for, " +
            "and what done looks like.",
        ["suggestedBudget"] = 1000,
        ["priceRationale"] = DemoNotice,
        ["milestones"] = new JsonArray(
            Milestone("Plan and design", "Agree the scope and the design.", 300, 7),
            Milestone("Build", "Build the main work.", 500, 21),
            Milestone("Test and hand over", "Fix problems and hand over.", 200, 28)),
        ["assumptions"] = new JsonArray(DemoNotice),
        ["questionsForClient"] = new JsonArray(
            "What does a finished result look like?")
    };

    private static JsonObject Milestone(
        string title,
        string description,
        decimal amount,
        int dueInDays) => new()
    {
        ["title"] = title,
        ["description"] = description,
        ["amount"] = amount,
        ["dueInDays"] = dueInDays
    };

    private static JsonObject MilestoneReview() => new()
    {
        ["summary"] = DemoNotice,
        ["recommendation"] = "InsufficientEvidence",
        ["confidence"] = 0.5,
        ["criteria"] = new JsonArray(new JsonObject
        {
            ["requirement"] = "The milestone description",
            ["status"] = "unclear",
            ["evidence"] = DemoNotice
        }),
        ["suggestedFeedback"] = DemoNotice,
        ["limitations"] = new JsonArray(DemoNotice)
    };

    private static JsonObject ProfileCoach() => new()
    {
        ["completenessScore"] = 50,
        ["summary"] = DemoNotice,
        ["missing"] = new JsonArray(new JsonObject
        {
            ["field"] = "About",
            ["advice"] = DemoNotice
        }),
        ["suggestedHeadline"] = "Freelancer (demo headline)",
        ["suggestedAbout"] = DemoNotice,
        ["suggestedSkills"] = new JsonArray("Demo skill"),
        ["suggestedPortfolioItems"] = new JsonArray()
    };

    private static JsonObject ResumeDraft() => new()
    {
        ["resumeMarkdown"] =
            $"# Demo resume\n\n{DemoNotice}\n\n## Experience\n- Add your work here.",
        ["notes"] = new JsonArray(DemoNotice)
    };

    private static JsonObject DisputeRuling() => new()
    {
        ["freelancerSharePercent"] = 50,
        ["confidence"] = 0.7,
        ["summary"] = DemoNotice,
        ["findings"] = new JsonArray(new JsonObject
        {
            ["requirement"] = "The milestone description",
            ["status"] = "unclear",
            ["evidence"] = DemoNotice
        }),
        ["reasoning"] = DemoNotice,
        ["manipulationAttempts"] = new JsonArray()
    };
}
