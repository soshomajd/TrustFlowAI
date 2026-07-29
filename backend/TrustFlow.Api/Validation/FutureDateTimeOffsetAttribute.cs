using System.ComponentModel.DataAnnotations;

namespace TrustFlow.Api.Validation;

[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter
)]
public sealed class FutureDateTimeOffsetAttribute
    : ValidationAttribute
{
    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        if (value is not DateTimeOffset deadline)
        {
            return new ValidationResult(
                "Deadline must be a valid date and time."
            );
        }

        if (deadline == default)
        {
            return new ValidationResult(
                "Deadline is required."
            );
        }

        if (deadline.ToUniversalTime() <=
            DateTimeOffset.UtcNow)
        {
            return new ValidationResult(
                "Deadline must be in the future."
            );
        }

        return ValidationResult.Success;
    }
}