using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Milestones;

public sealed class RequestMilestoneChangesRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 3)]
    public string Reason { get; init; } = string.Empty;
}
