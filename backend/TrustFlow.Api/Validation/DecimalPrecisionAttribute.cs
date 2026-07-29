using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Numerics;

namespace TrustFlow.Api.Validation;

[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Field |
    AttributeTargets.Parameter
)]
public sealed class DecimalPrecisionAttribute
    : ValidationAttribute
{
    public DecimalPrecisionAttribute(
        int precision,
        int scale)
    {
        if (precision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                "Precision must be greater than zero."
            );
        }

        if (scale < 0 || scale > precision)
        {
            throw new ArgumentOutOfRangeException(
                nameof(scale),
                "Scale must be between zero and precision."
            );
        }

        Precision = precision;
        Scale = scale;
    }

    public int Precision { get; }

    public int Scale { get; }

    protected override ValidationResult? IsValid(
        object? value,
        ValidationContext validationContext)
    {
        if (value is null)
        {
            return ValidationResult.Success;
        }

        if (value is not decimal decimalValue)
        {
            return new ValidationResult(
                $"{validationContext.DisplayName} must be a decimal number."
            );
        }

        var bits = decimal.GetBits(decimalValue);

        var actualScale =
            (bits[3] >> 16) & 0x7F;

        var unscaledValue =
            ((BigInteger)(uint)bits[2] << 64) |
            ((BigInteger)(uint)bits[1] << 32) |
            (uint)bits[0];

        var actualPrecision = unscaledValue.IsZero
            ? 1
            : unscaledValue
                .ToString(CultureInfo.InvariantCulture)
                .Length;

        var integerDigits = Math.Max(
            actualPrecision - actualScale,
            0
        );

        var maximumIntegerDigits =
            Precision - Scale;

        if (actualScale > Scale ||
            integerDigits > maximumIntegerDigits)
        {
            return new ValidationResult(
                ErrorMessage ??
                $"{validationContext.DisplayName} must have at most " +
                $"{maximumIntegerDigits} integer digits and " +
                $"{Scale} decimal places.",
                [validationContext.MemberName!]
            );
        }

        return ValidationResult.Success;
    }
}