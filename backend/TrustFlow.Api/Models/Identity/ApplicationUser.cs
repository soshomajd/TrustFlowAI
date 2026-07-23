using Microsoft.AspNetCore.Identity;

namespace TrustFlow.Api.Models.Identity;

using TrustFlow.Api.Models;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Project> ClientProjects { get; set; } = [];
    public ICollection<Proposal> FreelancerProposals { get; set; } = [];

}