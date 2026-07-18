using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Milestones;

public class UpdateMilestoneRequest
{
    [Required]
    [MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, double.MaxValue)]
    public decimal Amount { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceNumber { get; set; }

    public DateTime Deadline { get; set; }
}