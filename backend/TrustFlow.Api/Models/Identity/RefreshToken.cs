using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TrustFlow.Api.Models.Identity;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    [JsonIgnore]
    public ApplicationUser User { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public Guid FamilyId { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    [NotMapped]
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
    [NotMapped]
    public bool IsActive => RevokedAt is null && !IsExpired;
}