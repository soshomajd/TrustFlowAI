namespace TrustFlow.Api.Models;

public sealed class ProfilePhoto
{
    public Guid UserId { get; set; }
    public byte[] Content { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}
