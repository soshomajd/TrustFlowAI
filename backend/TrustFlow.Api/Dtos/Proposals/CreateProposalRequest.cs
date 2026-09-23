using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Proposals;

public class CreateProposalRequest
{
    [Required]
    [MaxLength(5000)]
    public string CoverLetter { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int EstimatedDays { get; set; }

    // The milestones this freelancer offers to deliver. One stage, a few of
    // them, or all of them; the price is the sum of those milestones.
    [Required]
    [MinLength(1)]
    public Guid[] MilestoneIds { get; set; } = [];
}