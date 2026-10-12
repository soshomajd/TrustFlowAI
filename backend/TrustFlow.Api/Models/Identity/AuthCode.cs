using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models.Identity;

// A short code mailed to a user to prove they own an address. Only a keyed
// hash is stored, so a database leak does not hand out working codes.
public class AuthCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public AuthCodePurpose Purpose { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public int FailedAttempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
