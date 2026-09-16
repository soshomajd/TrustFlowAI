using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Freelancers;

public sealed class UpdateFreelancerProfileRequest : IValidatableObject
{
    [MaxLength(100)]
    public string Headline { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string About { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    [Required]
    [RegularExpression("^(NotSpecified|Available|Limited|Unavailable)$")]
    public string Availability { get; set; } = "NotSpecified";

    [Required, MaxLength(10)]
    public string[] Skills { get; set; } = [];

    [Required, MaxLength(5)]
    public string[] Languages { get; set; } = [];

    [Required, MaxLength(3)]
    public PortfolioItemRequest[] PortfolioItems { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var (name, values) in new[] { (nameof(Skills), Skills), (nameof(Languages), Languages) })
        {
            if (values is not null && values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 40))
            {
                yield return new ValidationResult("Each entry must contain between 1 and 40 characters.", [name]);
            }
        }

        if (PortfolioItems is not null && PortfolioItems.Any(item => item is null))
        {
            yield return new ValidationResult("Portfolio items cannot be null.", [nameof(PortfolioItems)]);
        }
    }
}

public sealed class PortfolioItemRequest : IValidatableObject
{
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Url) &&
            (!Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
             string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)))
        {
            yield return new ValidationResult("Use a full http:// or https:// link without embedded credentials.", [nameof(Url)]);
        }
    }
}
