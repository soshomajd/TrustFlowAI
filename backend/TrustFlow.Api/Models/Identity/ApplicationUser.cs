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

}