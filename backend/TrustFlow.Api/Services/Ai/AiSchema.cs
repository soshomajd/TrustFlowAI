using System.Text.Json.Nodes;

namespace TrustFlow.Api.Services.Ai;

// Small builders for the JSON Schema each answer tool declares.
public static class AiSchema
{
    public static JsonObject Object(
        IEnumerable<(string Name, JsonObject Schema)> properties,
        params string[] required)
    {
        var props = new JsonObject();

        foreach (var (name, schema) in properties)
        {
            props[name] = schema;
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(
                required.Select(item => (JsonNode)item).ToArray())
        };
    }

    public static JsonObject Text(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    public static JsonObject Number(string description) => new()
    {
        ["type"] = "number",
        ["description"] = description
    };

    public static JsonObject Integer(string description) => new()
    {
        ["type"] = "integer",
        ["description"] = description
    };

    public static JsonObject OneOf(
        string description,
        params string[] values) => new()
    {
        ["type"] = "string",
        ["description"] = description,
        ["enum"] = new JsonArray(
            values.Select(item => (JsonNode)item).ToArray())
    };

    public static JsonObject ListOf(
        JsonObject items,
        string description) => new()
    {
        ["type"] = "array",
        ["description"] = description,
        ["items"] = items
    };
}
