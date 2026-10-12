using System.Text.Json.Nodes;
using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Ai;

// Turns a client's rough idea into a project they can create: a title, a
// description, a price and milestones. It only drafts. The client reads it,
// edits it, and creates the project themselves.
public sealed class ProjectDraftService(IAiGateway gateway)
{
    private const int MaximumMilestones = 8;

    private static readonly AiToolSchema Tool = new(
        "submit_project_draft",
        "Return the drafted project plan.",
        AiSchema.Object(
            [
                ("title", AiSchema.Text("Short project title, at most 200 characters.")),
                ("description", AiSchema.Text("Clear description: goal, audience, scope, what is out of scope, what done looks like. At most 5000 characters.")),
                ("suggestedBudget", AiSchema.Number("Total fair price in US dollar equivalents.")),
                ("priceRationale", AiSchema.Text("Why that price, and what would make it higher or lower.")),
                ("milestones", AiSchema.ListOf(
                    AiSchema.Object(
                        [
                            ("title", AiSchema.Text("Milestone title.")),
                            ("description", AiSchema.Text("What exactly will be delivered and how the client can check it.")),
                            ("amount", AiSchema.Number("Share of the budget for this milestone.")),
                            ("dueInDays", AiSchema.Integer("Days from project start until due."))
                        ],
                        "title", "description", "amount", "dueInDays"),
                    "One to eight milestones in delivery order.")),
                ("assumptions", AiSchema.ListOf(
                    AiSchema.Text("An assumption made."),
                    "Assumptions the plan rests on.")),
                ("questionsForClient", AiSchema.ListOf(
                    AiSchema.Text("A question."),
                    "Things the client should settle before posting."))
            ],
            "title", "description", "suggestedBudget", "milestones"));

    public async Task<ProjectDraftResponse> DraftAsync(
        Guid userId,
        ProjectDraftRequest request,
        CancellationToken cancellationToken)
    {
        var boundary = AiPrompt.NewBoundary();

        var system =
            "You help a client turn a rough idea into a clear freelance " +
            "project.\n\n" +
            "- Write in the same language as the client's idea.\n" +
            "- Never invent requirements the client did not imply. When " +
            "something important is unknown, say it in assumptions or " +
            "questionsForClient.\n" +
            "- Split the work into 1 to 8 milestones. Each must be a " +
            "deliverable the client can check, in order, small enough to " +
            "pay for on its own. Make the first one small so both sides " +
            "can test working together.\n" +
            "- Give a realistic price and say why. Prices are in US dollar " +
            "equivalents. If the client gave a budget, plan to that budget.\n" +
            "- Milestone amounts should add up to the total.\n\n" +
            AiPrompt.Safety(boundary);

        var content = new List<AiContentBlock>
        {
            AiPrompt.Fenced(boundary, "The client's idea", request.Idea, 3000)
        };

        if (request.BudgetHint is { } budgetHint)
        {
            content.Add(new AiTextBlock(
                $"The client's budget is {budgetHint}. Plan to that."));
        }

        if (request.DeadlineDays is { } deadlineDays)
        {
            content.Add(new AiTextBlock(
                $"The client wants everything done within {deadlineDays} days."));
        }

        var result = await gateway.RunAsync(
            userId,
            AiPurpose.ProjectDraft,
            system,
            content,
            Tool,
            output => Parse(output, request, gateway.Status.IsDemo),
            cancellationToken);

        return result.Value;
    }

    // Builds the final draft from the model's answer. Money is never taken
    // on trust: the amounts are rebuilt so they add up exactly to a budget
    // the client can read, whatever the model summed to.
    internal static ProjectDraftResponse Parse(
        JsonObject output,
        ProjectDraftRequest request,
        bool isDemo)
    {
        var rawMilestones = AiOutput.Objects(
            output,
            "milestones",
            MaximumMilestones);

        if (rawMilestones.Count == 0)
        {
            throw new AiModelException(
                "invalid_output",
                "The draft had no milestones.");
        }

        var budget = request.BudgetHint
            ?? Math.Round(
                AiOutput.Decimal(
                    output, "suggestedBudget", 0.01m, 1_000_000_000m),
                2);

        var raw = rawMilestones
            .Select(item => AiOutput.Decimal(
                item, "amount", 0.01m, 1_000_000_000m))
            .ToArray();

        var amounts = Rescale(raw, budget);

        var milestones = new List<ProjectDraftMilestone>();
        var previousDue = 0;

        for (var index = 0; index < rawMilestones.Count; index++)
        {
            var item = rawMilestones[index];

            var due = AiOutput.Integer(item, "dueInDays", 1, 730);

            // Due dates only move forward, and never past the client's
            // own limit.
            due = Math.Max(due, previousDue + 1);

            if (request.DeadlineDays is { } limit)
            {
                due = Math.Min(due, limit);
            }

            previousDue = due;

            milestones.Add(new ProjectDraftMilestone
            {
                SequenceNumber = index + 1,
                Title = AiOutput.String(item, "title", 200),
                Description = AiOutput.String(item, "description", 5000),
                Amount = amounts[index],
                DueInDays = due
            });
        }

        return new ProjectDraftResponse
        {
            Title = AiOutput.String(output, "title", 200),
            Description = AiOutput.String(output, "description", 5000),
            Budget = budget,
            PriceRationale = AiOutput.String(
                output, "priceRationale", 1500, required: false),
            Milestones = milestones,
            Assumptions = AiOutput.Strings(
                output, "assumptions", 8, 300).ToList(),
            QuestionsForClient = AiOutput.Strings(
                output, "questionsForClient", 6, 300).ToList(),
            IsDemo = isDemo
        };
    }

    internal static decimal[] Rescale(decimal[] raw, decimal budget)
    {
        var total = raw.Sum();

        if (total <= 0)
        {
            throw new AiModelException(
                "invalid_output",
                "The milestone amounts were not usable.");
        }

        var scaled = raw
            .Select(amount => Math.Round(
                amount / total * budget,
                2,
                MidpointRounding.ToZero))
            .ToArray();

        // Rounding down loses a few cents; the last milestone carries them
        // so the sum is exactly the budget.
        scaled[^1] += budget - scaled.Sum();

        if (scaled.Any(amount => amount < 0.01m))
        {
            throw new AiModelException(
                "invalid_output",
                "The budget is too small for this many milestones.");
        }

        return scaled;
    }
}
