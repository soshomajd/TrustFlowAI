namespace TrustFlow.Api.Dtos.Clients;

public sealed class ClientProfileResponse
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string AccountType { get; init; } = "Individual";
    public string CompanyName { get; init; } = string.Empty;
    public string Overview { get; init; } = string.Empty;
    public string Industry { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Website { get; init; } = string.Empty;
    public string CompanySize { get; init; } = "NotSpecified";
    public DateTimeOffset MemberSince { get; init; }
    public bool WalletVerified { get; init; }
    public bool IdentityVerified { get; init; }
    public DateTimeOffset? IdentityVerifiedAt { get; init; }
    public string VerificationStatus { get; init; } = "NotSubmitted";
    public DateTimeOffset? VerificationSubmittedAt { get; init; }
    public string VerificationRejectionReason { get; init; } = string.Empty;
    public int ProjectsPosted { get; init; }
    public int ActiveProjects { get; init; }
    public int CompletedProjects { get; init; }
    public int TotalHires { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
