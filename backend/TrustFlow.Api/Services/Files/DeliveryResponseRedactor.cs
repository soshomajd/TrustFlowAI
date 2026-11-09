using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Data;

namespace TrustFlow.Api.Services.Files;

// Stored opinions and evidence remain immutable. Redact known locked source
// URLs at the participant response boundary, including an old AI answer which
// happened to repeat one. This is not an external-link fetch or proxy.
public sealed class DeliveryResponseRedactor(AppDbContext db, DeliveryOriginalAccessPolicy access)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> ForClientAsync<T>(T response, Guid projectId, Guid milestoneId,
        CancellationToken cancellationToken)
    {
        var sources = await db.MilestoneSubmissions.AsNoTracking()
            .Where(item => item.MilestoneId == milestoneId && item.Milestone.ProjectId == projectId &&
                item.SourceFilesLink != null)
            .Select(item => new { item.FreelancerId, item.SourceFilesLink })
            .Distinct().ToListAsync(cancellationToken);
        if (sources.Count == 0) return response;
        var protectedUrls = new List<string>();
        foreach (var group in sources.GroupBy(item => item.FreelancerId))
        {
            if (await access.ClientHasOriginalsAsync(projectId, milestoneId, group.Key, cancellationToken)) continue;
            protectedUrls.AddRange(group.Select(item => item.SourceFilesLink!).Where(item => item.Length > 0));
        }
        if (protectedUrls.Count == 0) return response;
        var node = JsonSerializer.SerializeToNode(response, Json);
        Redact(node, protectedUrls);
        return node!.Deserialize<T>(Json)!;
    }

    private static void Redact(JsonNode? node, IReadOnlyList<string> urls)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                {
                    // A URL-valued property must be absent rather than an
                    // invalid clickable placeholder after redaction.
                    if (property.Key.EndsWith("Link", StringComparison.OrdinalIgnoreCase) &&
                        urls.Any(url => string.Equals(text, url, StringComparison.OrdinalIgnoreCase)))
                        obj[property.Key] = null;
                    else obj[property.Key] = Clean(text, urls);
                }
                else Redact(property.Value, urls);
            }
        }
        else if (node is JsonArray array)
        {
            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is JsonValue value && value.TryGetValue<string>(out var text))
                    array[index] = Clean(text, urls);
                else Redact(array[index], urls);
            }
        }
    }

    private static string Clean(string text, IReadOnlyList<string> urls)
    {
        foreach (var url in urls.OrderByDescending(item => item.Length))
            text = text.Replace(url, "[Protected source files]", StringComparison.OrdinalIgnoreCase);
        return text;
    }
}
