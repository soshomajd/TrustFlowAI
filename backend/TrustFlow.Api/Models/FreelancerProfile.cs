namespace TrustFlow.Api.Models;

public sealed class FreelancerProfile
{
    public Guid UserId { get; set; }
    public string Headline { get; set; } = string.Empty;
    public string About { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int? YearsOfExperience { get; set; }
    public string Availability { get; set; } = "NotSpecified";
    public string[] Skills { get; set; } = [];
    public string[] Languages { get; set; } = [];
    public string PortfolioItemsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedAt { get; set; }
}
