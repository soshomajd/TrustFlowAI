using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Ai;
using TrustFlow.Api.Dtos.Freelancers;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Services.Ai;

// Helps a freelancer finish their profile and write a resume from what is
// true: the profile they wrote, the work they finished here, the resume they
// uploaded, and anything they add. It suggests; the freelancer reads,
// edits and saves. It never changes a profile on its own.
public sealed class FreelancerCoachService(
    AppDbContext dbContext,
    IAiGateway gateway)
{
    private static readonly AiToolSchema CoachTool = new(
        "submit_profile_coaching",
        "Return the profile review and suggested wording.",
        AiSchema.Object(
            [
                ("completenessScore", AiSchema.Integer("0 to 100: how complete and convincing the profile is.")),
                ("summary", AiSchema.Text("Two or three sentences on the profile's biggest strength and biggest gap.")),
                ("missing", AiSchema.ListOf(
                    AiSchema.Object(
                        [
                            ("field", AiSchema.Text("Which part of the profile.")),
                            ("advice", AiSchema.Text("What to add and why a client cares."))
                        ],
                        "field", "advice"),
                    "Up to eight gaps, most important first.")),
                ("suggestedHeadline", AiSchema.Text("A headline of at most 100 characters.")),
                ("suggestedAbout", AiSchema.Text("An About text of at most 1200 characters.")),
                ("suggestedSkills", AiSchema.ListOf(
                    AiSchema.Text("A skill of at most 40 characters."),
                    "Up to ten skills that the facts support.")),
                ("suggestedPortfolioItems", AiSchema.ListOf(
                    AiSchema.Object(
                        [
                            ("title", AiSchema.Text("Title of at most 100 characters.")),
                            ("description", AiSchema.Text("What was done and the result, at most 500 characters."))
                        ],
                        "title", "description"),
                    "Up to three, taken from finished work on the platform."))
            ],
            "completenessScore", "summary", "missing"));

    private static readonly AiToolSchema ResumeTool = new(
        "submit_resume",
        "Return the resume.",
        AiSchema.Object(
            [
                ("resumeMarkdown", AiSchema.Text("The full resume in Markdown.")),
                ("notes", AiSchema.ListOf(
                    AiSchema.Text("A gap the freelancer should fill in."),
                    "What is missing or left as a placeholder."))
            ],
            "resumeMarkdown"));

    public async Task<ProfileCoachResponse> CoachAsync(
        Guid freelancerId,
        CancellationToken cancellationToken)
    {
        var boundary = AiPrompt.NewBoundary();

        var system =
            "You are a career coach helping a freelancer make their " +
            "profile convincing to clients.\n\n" +
            "- Use only facts in the input. Never invent employers, " +
            "degrees, years, skills, clients, results or links. If " +
            "something would help but is not there, list it under " +
            "missing instead of making it up.\n" +
            "- Suggested wording must be something this person could " +
            "honestly sign. Keep it plain and specific, not hype.\n" +
            "- Write in the language the freelancer used.\n" +
            "- Portfolio items may only come from finished work listed " +
            "under work completed on the platform.\n\n" +
            AiPrompt.Safety(boundary);

        var content = await BuildFactsAsync(
            freelancerId,
            boundary,
            extraFacts: null,
            cancellationToken);

        var isDemo = gateway.Status.IsDemo;

        var result = await gateway.RunAsync(
            freelancerId,
            AiPurpose.FreelancerProfileCoach,
            system,
            content,
            CoachTool,
            output => ParseCoach(output, isDemo),
            cancellationToken);

        return result.Value;
    }

    public async Task<ResumeDraftResponse> DraftResumeAsync(
        Guid freelancerId,
        string? extraFacts,
        CancellationToken cancellationToken)
    {
        var boundary = AiPrompt.NewBoundary();

        var system =
            "You write a clear one to two page resume for a freelancer.\n\n" +
            "- Use only facts in the input. Never invent employers, " +
            "dates, degrees, skills or results. Where a section needs " +
            "something that is not given, write [add here] and list it in " +
            "notes.\n" +
            "- Put finished work from the platform under experience, with " +
            "what was delivered. Do not state payment amounts.\n" +
            "- Order: headline, summary, skills, experience, education, " +
            "languages. Leave out any section with nothing to put in it.\n" +
            "- Write in the language the freelancer used.\n\n" +
            AiPrompt.Safety(boundary);

        var content = await BuildFactsAsync(
            freelancerId,
            boundary,
            extraFacts,
            cancellationToken);

        var isDemo = gateway.Status.IsDemo;

        var result = await gateway.RunAsync(
            freelancerId,
            AiPurpose.FreelancerResumeDraft,
            system,
            content,
            ResumeTool,
            output => new ResumeDraftResponse
            {
                ResumeMarkdown = AiOutput.String(
                    output, "resumeMarkdown", 12_000),
                Notes = AiOutput.Strings(output, "notes", 8, 300).ToList(),
                IsDemo = isDemo
            },
            cancellationToken);

        return result.Value;
    }

    private async Task<List<AiContentBlock>> BuildFactsAsync(
        Guid freelancerId,
        string boundary,
        string? extraFacts,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.FreelancerProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.UserId == freelancerId,
                cancellationToken);

        var blocks = new List<AiContentBlock>();

        if (profile is null)
        {
            blocks.Add(new AiTextBlock(
                "The freelancer has not filled in their profile yet."));
        }
        else
        {
            var portfolio = new StringBuilder();

            foreach (var item in
                     JsonSerializer.Deserialize<PortfolioItemRequest[]>(
                         profile.PortfolioItemsJson) ?? [])
            {
                portfolio.AppendLine(
                    $"- {item.Title}: {item.Description} {item.Url}");
            }

            blocks.Add(AiPrompt.Fenced(
                boundary,
                "Profile written by the freelancer",
                $"Headline: {profile.Headline}\n" +
                $"About: {profile.About}\n" +
                $"Location: {profile.Location}\n" +
                $"Years of experience: {profile.YearsOfExperience}\n" +
                $"Availability: {profile.Availability}\n" +
                $"Skills: {string.Join(", ", profile.Skills)}\n" +
                $"Languages: {string.Join(", ", profile.Languages)}\n" +
                $"Portfolio:\n{portfolio}",
                4000));
        }

        // Finished work, by project. The client's name and the amounts are
        // left out on purpose.
        var finished = await dbContext.Milestones
            .AsNoTracking()
            .Where(item =>
                item.FreelancerId == freelancerId &&
                item.Status == MileStoneStatus.Approved)
            .OrderBy(item => item.ProjectId)
            .ThenBy(item => item.SequenceNumber)
            .Select(item => new
            {
                item.ProjectId,
                ProjectTitle = item.Project.Title,
                ProjectDescription = item.Project.Description,
                MilestoneTitle = item.Title
            })
            .ToListAsync(cancellationToken);

        if (finished.Count == 0)
        {
            blocks.Add(new AiTextBlock(
                "The freelancer has no finished work on the platform yet."));
        }
        else
        {
            var work = new StringBuilder();

            foreach (var project in finished.GroupBy(item => item.ProjectId)
                         .Take(10))
            {
                var first = project.First();

                work.AppendLine(
                    $"Project: {first.ProjectTitle}\n" +
                    $"Brief: {AiPrompt.Truncate(first.ProjectDescription, 600)}\n" +
                    "Delivered and approved: " +
                    string.Join("; ", project.Select(item =>
                        item.MilestoneTitle)) +
                    "\n");
            }

            blocks.Add(AiPrompt.Fenced(
                boundary,
                "Work completed on the platform (each delivery was " +
                "approved by its client)",
                work.ToString(),
                8000));
        }

        var resume = await dbContext.FreelancerResumes
            .AsNoTracking()
            .Where(item => item.UserId == freelancerId)
            .Select(item => item.Content)
            .SingleOrDefaultAsync(cancellationToken);

        if (resume is not null)
        {
            var text = AiEvidenceBuilder.ExtractPdfText(
                resume,
                out var problem);

            blocks.Add(text is null
                ? new AiTextBlock(
                    $"A resume PDF was uploaded but {problem}")
                : AiPrompt.Fenced(
                    boundary,
                    "Text of the resume the freelancer uploaded",
                    text,
                    8000));
        }

        if (!string.IsNullOrWhiteSpace(extraFacts))
        {
            blocks.Add(AiPrompt.Fenced(
                boundary,
                "Extra facts the freelancer added",
                extraFacts,
                3000));
        }

        return blocks;
    }

    internal static ProfileCoachResponse ParseCoach(
        JsonObject output,
        bool isDemo)
    {
        var skills = AiOutput.Strings(output, "suggestedSkills", 10, 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ProfileCoachResponse
        {
            CompletenessScore = AiOutput.Integer(
                output, "completenessScore", 0, 100),
            Summary = AiOutput.String(output, "summary", 800),
            Missing = AiOutput.Objects(output, "missing", 8)
                .Select(item => new ProfileGapResponse
                {
                    Field = AiOutput.String(item, "field", 80),
                    Advice = AiOutput.String(item, "advice", 400)
                })
                .ToList(),
            SuggestedHeadline = AiOutput.String(
                output, "suggestedHeadline", 100, required: false),
            SuggestedAbout = AiOutput.String(
                output, "suggestedAbout", 1200, required: false),
            SuggestedSkills = skills,
            SuggestedPortfolioItems = AiOutput
                .Objects(output, "suggestedPortfolioItems", 3)
                .Select(item => new SuggestedPortfolioItemResponse
                {
                    Title = AiOutput.String(item, "title", 100),
                    Description = AiOutput.String(
                        item, "description", 500, required: false)
                })
                .ToList(),
            IsDemo = isDemo
        };
    }
}
