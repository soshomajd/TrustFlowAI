namespace TrustFlow.Api.Models;

public sealed class FreelancerResume
{
    public Guid UserId { get; set; }
    public byte[] Content { get; set; } = [];
    public int FileSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
}
