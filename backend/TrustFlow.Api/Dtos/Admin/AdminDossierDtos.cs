using System.Globalization;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Dtos.Admin;

public sealed record AdminDossierResponse(Guid Id, string Title, string Description, ProjectStatus Status,
    Guid? ClientId, string ClientName, decimal Budget, DateTimeOffset Deadline, DateTimeOffset CreatedAt,
    int ParticipantCount, int MilestoneCount, int ProposalCount, int EscrowCount, int DeliveryCount)
{
    public string BudgetExact => Budget.ToString(CultureInfo.InvariantCulture);
}

public sealed record AdminDossierParticipant(Guid Id, string FullName, bool IsClient,
    bool CurrentFreelancer, bool HasEscrow, bool HasProposal, string? WalletAddress, bool WalletVerified, bool HasDelivery)
{
    public string Kind => "participant";
}

public sealed record AdminDeliveryAttachment(Guid Id, string FileName, string ContentType, int FileSize, string Sha256);
public sealed record AdminDossierDelivery(Guid Id, Guid MilestoneId, string MilestoneTitle, int SequenceNumber,
    MileStoneStatus CurrentMilestoneStatus, Guid FreelancerId, string FreelancerName, int Version, bool IsLatest,
    string Note, string? PreviewLink, string? SourceFilesLink, string? ClientFeedback,
    DateTimeOffset SubmittedAt, DateTimeOffset? ReviewedAt, IReadOnlyList<AdminDeliveryAttachment> Attachments)
{
    public string Kind => "delivery";
}

public sealed record AdminDossierMilestone(Guid Id, string Title, string Description, int SequenceNumber,
    MileStoneStatus Status, decimal Amount, DateTimeOffset Deadline, Guid? FreelancerId, string? FreelancerName)
{
    public string Kind => "milestone";
    public string AmountExact => Amount.ToString(CultureInfo.InvariantCulture);
}

public sealed record AdminDossierProposal(Guid Id, Guid FreelancerId, string FreelancerName,
    ProposalStatus Status, string CoverLetter, decimal BidAmount, int EstimatedDays,
    int MilestoneCount, DateTimeOffset CreatedAt)
{
    public string Kind => "proposal";
    public string BidAmountExact => BidAmount.ToString(CultureInfo.InvariantCulture);
}

public sealed record AdminDossierEscrow(Guid Id, Guid FreelancerId, string FreelancerName,
    EscrowStatus Status, long ChainId, string TokenAddress, string? ContractAddress,
    string? ClientWalletAddress, string? FreelancerWalletAddress, string? ArbiterWalletAddress,
    string? FeeRecipientAddress, int PlatformFeeBasisPoints, int? TokenDecimals,
    decimal TotalAmount, decimal ReleasedAmount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public string Kind => "escrow";
    public string TotalAmountExact => TotalAmount.ToString(CultureInfo.InvariantCulture);
    public string ReleasedAmountExact => ReleasedAmount.ToString(CultureInfo.InvariantCulture);
}
