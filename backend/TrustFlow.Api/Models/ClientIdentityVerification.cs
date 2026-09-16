namespace TrustFlow.Api.Models;

public sealed class ClientIdentityVerification
{
    public Guid ClientId { get; set; }
    public string AccountType { get; set; } = "Individual";
    public byte[]? LegalNameProtected { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public byte[]? RegistrationNumberProtected { get; set; }
    public byte[]? DocumentContentProtected { get; set; }
    public int? DocumentFileSize { get; set; }
    public string DocumentSha256 { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string RejectionReason { get; set; } = string.Empty;
}
