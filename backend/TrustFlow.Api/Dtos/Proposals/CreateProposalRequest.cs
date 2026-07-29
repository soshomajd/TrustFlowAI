using System.ComponentModel.DataAnnotations;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Proposals;

public class CreateProposalRequest
{
    [Required]
    [MaxLength(5000)]
    public string CoverLetter { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "9999999999999999.99"
    )]
    [DecimalPrecision(18, 2)]
    public decimal BidAmount { get; set; }

    [Range(1, int.MaxValue)]
    public int EstimatedDays { get; set; }
}