using Microsoft.AspNetCore.Identity;

namespace TrustFlow.Api.Models.Identity;

using TrustFlow.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Project> ClientProjects { get; set; } = [];
    public ICollection<Proposal> FreelancerProposals { get; set; } = [];
    public ICollection<Project> FreelancerProjects { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public string? WalletAddress { get; set; }

    public string? WalletAddressNormalized
    {
        get;
        set;
    }

    public DateTimeOffset? WalletVerifiedAt
    {
        get;
        set;
    }

    public string? PendingWalletAddress
    {
        get;
        set;
    }

    public string? WalletVerificationNonce
    {
        get;
        set;
    }

    public DateTimeOffset?
        WalletVerificationExpiresAt
    {
        get;
        set;
    }

}