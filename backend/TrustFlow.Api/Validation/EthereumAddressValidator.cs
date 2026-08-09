namespace TrustFlow.Api.Validation;

public static class EthereumAddressValidator
{
    public static bool IsValid(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var address = value.Trim();

        if (address.Length != 42)
        {
            return false;
        }

        if (!address.StartsWith(
                "0x",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var index = 2;
             index < address.Length;
             index++)
        {
            if (!Uri.IsHexDigit(address[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static string Normalize(
        string value)
    {
        return value
            .Trim()
            .ToLowerInvariant();
    }
}