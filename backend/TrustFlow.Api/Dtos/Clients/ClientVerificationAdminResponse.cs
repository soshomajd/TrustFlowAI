namespace TrustFlow.Api.Dtos.Clients;

public sealed class ClientVerificationAdminResponse
{
    public Guid ClientId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string AccountType { get; init; } = string.Empty;
    public string LegalName { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    public int DocumentFileSize { get; init; }
    public DateTimeOffset SubmittedAt { get; init; }
}

public sealed class ReviewClientVerificationRequest
{
    public bool Approve { get; init; }
    public string Reason { get; init; } = string.Empty;
}
