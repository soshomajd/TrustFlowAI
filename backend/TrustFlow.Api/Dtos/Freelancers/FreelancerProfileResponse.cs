namespace TrustFlow.Api.Dtos.Freelancers;

public sealed class FreelancerProfileResponse
{
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public DateTimeOffset MemberSince { get; init; }
    public bool WalletVerified { get; init; }
    public string Headline { get; init; } = string.Empty;
    public string About { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int? YearsOfExperience { get; init; }
    public string Availability { get; init; } = "NotSpecified";
    public string[] Skills { get; init; } = [];
    public string[] Languages { get; init; } = [];
    public PortfolioItemRequest[] PortfolioItems { get; init; } = [];
    public int CompletedProjectCount { get; init; }
    public CompletedFreelancerProjectResponse[] CompletedProjects { get; init; } = [];
    public DateTimeOffset? UpdatedAt { get; init; }
    public bool HasResume { get; init; }
    public int? ResumeFileSize { get; init; }
    public DateTimeOffset? ResumeUploadedAt { get; init; }
}

public sealed class CompletedFreelancerProjectResponse
{
    public string Title { get; init; } = string.Empty;
    public int MilestoneCount { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
