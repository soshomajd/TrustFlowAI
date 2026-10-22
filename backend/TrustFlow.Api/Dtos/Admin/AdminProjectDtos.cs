using System.ComponentModel.DataAnnotations;
using TrustFlow.Api.Dtos.Escrows;
using TrustFlow.Api.Dtos.Milestones;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Admin;

public class AdminPageQuery
{
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class AdminProjectQuery : AdminPageQuery, IValidatableObject
{
    [StringLength(200)] public string? Search { get; set; }
    [EnumDataType(typeof(ProjectStatus))]
    public ProjectStatus? Status { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? FreelancerId { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Search) && Search.Trim().Length < 3)
            yield return new ValidationResult("Search needs at least 3 characters.", [nameof(Search)]);
        if (From.HasValue && To.HasValue && From > To)
            yield return new ValidationResult("From must not be after To.", [nameof(From), nameof(To)]);
    }
}

public sealed record AdminProjectFreelancerResponse(Guid Id, string FullName);
public sealed record AdminMemberResponse(Guid Id, string FullName, string? WalletAddress, bool WalletVerified);
public sealed record AdminProjectSummaryResponse(Guid Id, string Title, ProjectStatus Status,
    Guid? ClientId, string ClientName, string BudgetExact, DateTimeOffset CreatedAt,
    int MilestoneCount, int EscrowCount, int ProposalCount, int OpenCaseCount,
    IReadOnlyList<AdminProjectFreelancerResponse> Freelancers);
public sealed record AdminThreadResponse(Guid Id, Guid FreelancerId, string FreelancerName, int MessageCount,
    DateTimeOffset CreatedAt, DateTimeOffset LastMessageAt);
public sealed record AdminEventResponse(string Kind, DateTimeOffset At, string Description, Guid? ResourceId);
public sealed record AdminProjectDetailResponse(AdminProjectSummaryResponse Project, string Description,
    DateTimeOffset Deadline, IReadOnlyList<AdminMemberResponse> Participants,
    IReadOnlyList<MilestoneResponse> Milestones, IReadOnlyList<EscrowResponse> Escrows,
    IReadOnlyList<EscrowCaseResponse> Cases, IReadOnlyList<AdminThreadResponse> Threads,
    IReadOnlyList<AdminEventResponse> Events);
public sealed record AdminDeliveryResponse(Guid FreelancerId, string FreelancerName, MilestoneSubmissionResponse Delivery);
public sealed record AdminMessageResponse(Guid Id, Guid SenderId, string SenderName, string Body, DateTimeOffset SentAt);
public sealed record AdminMessagesResponse(IReadOnlyList<AdminMessageResponse> Items, bool HasOlderMessages);
public sealed record AdminAccessResponse(Guid Id, Guid AdminId, string AdminName, Guid? ProjectId,
    Guid? ResourceId, string Action, DateTimeOffset CreatedAt);
