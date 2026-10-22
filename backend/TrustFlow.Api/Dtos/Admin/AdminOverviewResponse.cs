namespace TrustFlow.Api.Dtos.Admin;

public sealed record AdminOverviewResponse(
    int TotalProjects,
    int OpenProjects,
    int InProgressProjects,
    int CompletedProjects,
    int CancelledProjects,
    int TotalEscrows,
    int OpenCases,
    DateTimeOffset GeneratedAt);
