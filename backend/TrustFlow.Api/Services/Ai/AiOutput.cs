using System.Globalization;
using System.Text.Json.Nodes;

namespace TrustFlow.Api.Services.Ai;

// Reads the model's answer defensively. The model is not trusted to follow
// the schema it was given: every field is checked, bounded, and cleaned
// before it can reach a user or a form.
public static class AiOutput
{
    public static string String(
        JsonObject source,
        string name,
        int maximumLength,
        bool required = true)
    {
        var value = source[name] is JsonValue json &&
                    json.TryGetValue<string>(out var text)
            ? text.Trim()
            : string.Empty;

        if (required && value.Length == 0)
        {
            throw Invalid($"'{name}' is missing.");
        }

        return value.Length > maximumLength
            ? value[..maximumLength].TrimEnd()
            : value;
    }

    public static int Integer(
        JsonObject source,
        string name,
        int minimum,
        int maximum)
    {
        var number = ReadNumber(source, name);

        return (int)Math.Clamp(
            Math.Round(number, MidpointRounding.AwayFromZero),
            minimum,
            maximum);
    }

    public static decimal Decimal(
        JsonObject source,
        string name,
        decimal minimum,
        decimal maximum) =>
        Math.Clamp(ReadNumber(source, name), minimum, maximum);

    // A number can arrive as an integer, a decimal, a float or, from a
    // careless model, a quoted string. All of them read the same way, and
    // anything else is refused rather than guessed at.
    private static decimal ReadNumber(JsonObject source, string name)
    {
        if (source[name] is not JsonValue json)
        {
            throw Invalid($"'{name}' is missing.");
        }

        var text = json.TryGetValue<string>(out var quoted)
            ? quoted.Trim()
            : json.ToJsonString();

        if (!decimal.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number))
        {
            throw Invalid($"'{name}' is not a number.");
        }

        return number;
    }

    public static IReadOnlyList<JsonObject> Objects(
        JsonObject source,
        string name,
        int maximumCount)
    {
        if (source[name] is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonObject>()
            .Take(maximumCount)
            .ToList();
    }

    public static IReadOnlyList<string> Strings(
        JsonObject source,
        string name,
        int maximumCount,
        int maximumLength)
    {
        if (source[name] is not JsonArray array)
        {
            return [];
        }

        return array
            .Select(item =>
                item is JsonValue value &&
                value.TryGetValue<string>(out var text)
                    ? text.Trim()
                    : string.Empty)
            .Where(text => text.Length > 0)
            .Select(text => text.Length > maximumLength
                ? text[..maximumLength].TrimEnd()
                : text)
            .Take(maximumCount)
            .ToList();
    }

    // A value restricted to a known set, compared without case.
    public static string OneOf(
        JsonObject source,
        string name,
        params string[] allowed)
    {
        var value = String(source, name, 64);

        return allowed.FirstOrDefault(option =>
                   string.Equals(
                       option,
                       value,
                       StringComparison.OrdinalIgnoreCase))
               ?? throw Invalid($"'{name}' is not an allowed value.");
    }

    private static AiModelException Invalid(string message) =>
        new("invalid_output", message);
}
