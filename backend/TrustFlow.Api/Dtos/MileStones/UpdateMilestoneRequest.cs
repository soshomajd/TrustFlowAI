using System.ComponentModel.DataAnnotations;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Milestones;

public class UpdateMilestoneRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(5000)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    [DecimalPrecision(18, 2)]
    public decimal Amount { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceNumber { get; set; }

    [FutureDateTimeOffset]
    public DateTimeOffset Deadline { get; set; }
}