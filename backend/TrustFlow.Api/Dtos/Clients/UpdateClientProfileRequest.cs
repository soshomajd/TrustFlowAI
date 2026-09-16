using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Dtos.Clients;

public sealed class UpdateClientProfileRequest : IValidatableObject
{
    [Required]
    [RegularExpression("^(Individual|Company)$")]
    public string AccountType { get; init; } = "Individual";

    [MaxLength(120)]
    public string CompanyName { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Overview { get; init; } = string.Empty;

    [MaxLength(80)]
    public string Industry { get; init; } = string.Empty;

    [MaxLength(100)]
    public string Location { get; init; } = string.Empty;

    [MaxLength(500)]
    public string Website { get; init; } = string.Empty;

    [Required]
    [RegularExpression("^(NotSpecified|JustMe|2-10|11-50|51-200|201Plus)$")]
    public string CompanySize { get; init; } = "NotSpecified";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AccountType == "Company" && string.IsNullOrWhiteSpace(CompanyName))
            yield return new ValidationResult("Company name is required for company accounts.", [nameof(CompanyName)]);

        if (!string.IsNullOrWhiteSpace(Website) && !IsSafeWebsite(Website))
            yield return new ValidationResult("Website must be an HTTP(S) URL without embedded credentials.", [nameof(Website)]);
    }

    private static bool IsSafeWebsite(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo);
}
