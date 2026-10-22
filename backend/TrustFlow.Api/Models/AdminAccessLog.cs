namespace TrustFlow.Api.Models;

// Retained independently of project/user deletion; contains no message or file content.
public sealed class AdminAccessLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdminId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ResourceId { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
