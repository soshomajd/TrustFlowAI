using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;
using UglyToad.PdfPig;

namespace TrustFlow.Api.Services.Ai;

public sealed record EvidenceSection(string Label, string Text);

public sealed record EvidenceImage(
    string Label,
    string MediaType,
    byte[] Data);

// What the model is shown about one milestone: the requirement, each
// delivery, and the feedback between them. People appear only by role, and
// the hash lets a ruling be tied to exactly this evidence afterwards.
public sealed class AiEvidence
{
    public List<EvidenceSection> Sections { get; } = [];

    public List<EvidenceImage> Images { get; } = [];

    // Things the model could not see. They are shown to the model so it
    // does not pretend otherwise, and to the user so they know the limits.
    public List<string> Limitations { get; } = [];

    public string Sha256()
    {
        using var hash = IncrementalHash.CreateHash(
            HashAlgorithmName.SHA256);

        foreach (var section in Sections)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(
                $"{section.Label}\0{section.Text}\0"));
        }

        foreach (var image in Images)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(image.Label));
            hash.AppendData(image.Data);
        }

        return Convert.ToHexString(hash.GetHashAndReset())
            .ToLowerInvariant();
    }

    public List<AiContentBlock> ToBlocks(
        string boundary,
        int maximumCharactersPerSection)
    {
        var blocks = new List<AiContentBlock>();

        foreach (var section in Sections)
        {
            blocks.Add(AiPrompt.Fenced(
                boundary,
                section.Label,
                section.Text,
                maximumCharactersPerSection));
        }

        foreach (var image in Images)
        {
            blocks.Add(new AiTextBlock(
                $"Image attached to the delivery: {image.Label}"));

            blocks.Add(new AiImageBlock(image.MediaType, image.Data));
        }

        if (Limitations.Count > 0)
        {
            blocks.Add(new AiTextBlock(
                "What could not be inspected:\n" +
                string.Join("\n", Limitations.Select(item => $"- {item}"))));
        }

        return blocks;
    }
}

public sealed class AiEvidenceBuilder(AppDbContext dbContext)
{
    private const int MaximumSubmissions = 5;

    private const int MaximumTotalCharacters = 90_000;

    private const int MaximumPdfCharacters = 15_000;

    private const int MaximumPdfBytes = 10 * 1024 * 1024;

    private const int MaximumImages = 4;

    private const int MaximumImageBytes = 3 * 1024 * 1024;

    public async Task<AiEvidence?> BuildForMilestoneAsync(
        Guid projectId,
        Guid milestoneId,
        CancellationToken cancellationToken,
        Guid? deliveredBy = null)
    {
        var milestone = await dbContext.Milestones
            .AsNoTracking()
            .Where(item =>
                item.Id == milestoneId && item.ProjectId == projectId)
            .Select(item => new
            {
                item.Title,
                item.Description,
                item.Amount,
                item.SequenceNumber,
                item.Deadline,
                ProjectTitle = item.Project.Title,
                ProjectDescription = item.Project.Description
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (milestone is null)
        {
            return null;
        }

        var evidence = new AiEvidence();
        var characters = 0;

        void Add(string label, string text)
        {
            characters += text.Length;
            evidence.Sections.Add(new EvidenceSection(label, text));
        }

        Add(
            "Project description (written by the client)",
            $"{milestone.ProjectTitle}\n\n{milestone.ProjectDescription}");

        Add(
            "Milestone requirement (written by the client before any work)",
            $"Title: {milestone.Title}\n" +
            $"Order in project: {milestone.SequenceNumber}\n" +
            $"Amount: {milestone.Amount}\n" +
            $"Deadline: {milestone.Deadline:yyyy-MM-dd}\n\n" +
            milestone.Description);

        // Only the freelancer who holds the milestone has delivered for it.
        // A stage handed on after a resignation must not show the earlier
        // freelancer's work to its next owner.
        var submissions = await dbContext.MilestoneSubmissions
            .AsNoTracking()
            .Where(item =>
                item.MilestoneId == milestoneId &&
                (deliveredBy == null
                    ? item.FreelancerId == item.Milestone.FreelancerId
                    : item.FreelancerId == deliveredBy))
            .OrderByDescending(item => item.Version)
            .Take(MaximumSubmissions)
            .Select(item => new
            {
                item.Id,
                item.Version,
                item.Note,
                item.PreviewLink,
                item.SourceFilesLink,
                item.ClientFeedback,
                item.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        foreach (var submission in submissions.OrderBy(item => item.Version))
        {
            var links = new StringBuilder();

            if (submission.PreviewLink is not null)
            {
                links.AppendLine(
                    $"Preview link given: {submission.PreviewLink} " +
                    "(the system did not open it)");
            }

            if (submission.SourceFilesLink is not null)
            {
                links.AppendLine(
                    $"Source files link given: {submission.SourceFilesLink} " +
                    "(the system did not open it)");
            }

            Add(
                $"Delivery version {submission.Version} " +
                "(written by the freelancer)",
                $"Submitted: {submission.SubmittedAt:yyyy-MM-dd HH:mm} UTC\n" +
                $"{links}\n{submission.Note}");

            if (!string.IsNullOrWhiteSpace(submission.ClientFeedback))
            {
                Add(
                    $"Client feedback after delivery version " +
                    $"{submission.Version} (written by the client)",
                    submission.ClientFeedback);
            }

            if (submission.PreviewLink is not null ||
                submission.SourceFilesLink is not null)
            {
                evidence.Limitations.Add(
                    $"Links in delivery version {submission.Version} " +
                    "were not opened, so work shown only there could " +
                    "not be checked.");
            }

            var attachments = await dbContext
                .MilestoneSubmissionAttachments
                .AsNoTracking()
                .Where(item => item.SubmissionId == submission.Id)
                .Select(item => new
                {
                    item.OriginalFileName,
                    item.ContentType,
                    item.FileSize,
                    item.Content
                })
                .ToListAsync(cancellationToken);

            foreach (var attachment in attachments)
            {
                var name =
                    $"{attachment.OriginalFileName} " +
                    $"(delivery version {submission.Version})";

                if (characters >= MaximumTotalCharacters)
                {
                    evidence.Limitations.Add(
                        $"{name} was left out: the evidence was too long.");

                    continue;
                }

                if (attachment.ContentType == "application/pdf")
                {
                    var text = ExtractPdfText(
                        attachment.Content,
                        out var problem);

                    if (text is null)
                    {
                        evidence.Limitations.Add($"{name}: {problem}");
                    }
                    else
                    {
                        Add($"Text of attached PDF {name}", text);
                    }
                }
                else if (attachment.ContentType.StartsWith(
                             "image/",
                             StringComparison.Ordinal))
                {
                    if (evidence.Images.Count >= MaximumImages ||
                        attachment.FileSize > MaximumImageBytes)
                    {
                        evidence.Limitations.Add(
                            $"Image {name} was not shown: too many or " +
                            "too large.");
                    }
                    else
                    {
                        evidence.Images.Add(new EvidenceImage(
                            name,
                            attachment.ContentType,
                            attachment.Content));
                    }
                }
            }
        }

        if (submissions.Count == 0)
        {
            evidence.Limitations.Add(
                "The freelancer has not submitted anything yet.");
        }

        return evidence;
    }

    // Anything the file cannot give up as text is reported, not guessed at.
    internal static string? ExtractPdfText(
        byte[] content,
        out string? problem)
    {
        problem = null;

        if (content.Length > MaximumPdfBytes)
        {
            problem = "this PDF is too large to read.";

            return null;
        }

        try
        {
            using var document = PdfDocument.Open(content);

            var text = new StringBuilder();

            foreach (var page in document.GetPages().Take(40))
            {
                text.AppendLine(page.Text);

                if (text.Length >= MaximumPdfCharacters)
                {
                    break;
                }
            }

            var result = text.ToString().Trim();

            if (result.Length == 0)
            {
                problem =
                    "this PDF holds no readable text, for example a scan.";

                return null;
            }

            return result.Length > MaximumPdfCharacters
                ? result[..MaximumPdfCharacters]
                : result;
        }
        catch (Exception)
        {
            problem = "this PDF could not be read.";

            return null;
        }
    }
}
