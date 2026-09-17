using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Milestones;

public sealed class SubmitMilestoneRequest
{
    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Note { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? PreviewLink { get; init; }

    [StringLength(1000)]
    public string? SourceFilesLink { get; init; }

    public List<IFormFile> Files { get; init; } = [];
}
