namespace TrustFlow.Api.Models;

public sealed class ClientProfile
{
    public Guid UserId { get; set; }
    public string AccountType { get; set; } = "Individual";
    public string CompanyName { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string CompanySize { get; set; } = "NotSpecified";
    public DateTimeOffset UpdatedAt { get; set; }
}
