using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Proposals;

public class CreateProposalRequest
{
    [Required]
    [MaxLength(5000)]
    public string CoverLetter { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue)]
    public decimal BidAmount { get; set; }

    [Range(1, int.MaxValue)]
    public int EstimatedDays { get; set; }
}