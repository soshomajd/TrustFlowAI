using System.ComponentModel.DataAnnotations;
using TrustFlow.Api.Validation;

namespace TrustFlow.Api.Dtos.Projects;

public sealed class ProjectMarketplaceQuery
    : IValidatableObject
{
    [Range(1, 500)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [MaxLength(100)]
    public string? Search { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99"
    )]
    [DecimalPrecision(18, 2)]
    public decimal? MinBudget { get; set; }

    [Range(
        typeof(decimal),
        "0",
        "9999999999999999.99"
    )]
    [DecimalPrecision(18, 2)]
    public decimal? MaxBudget { get; set; }

    public DateTimeOffset? DeadlineBefore { get; set; }

    public ProjectSortOption SortBy { get; set; } =
        ProjectSortOption.Newest;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (MinBudget.HasValue &&
            MaxBudget.HasValue &&
            MinBudget.Value > MaxBudget.Value)
        {
            yield return new ValidationResult(
                "Minimum budget cannot be greater than maximum budget.",
                [
                    nameof(MinBudget),
                    nameof(MaxBudget)
                ]
            );
        }
    }
}