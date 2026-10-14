using System.Text.Json.Serialization;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Models;

public enum EscrowCaseKind
{
    // The freelancer says the milestone is delivered and asks to be paid.
    PaymentRequest = 0,

    // The client says the milestone was not delivered and asks for it back.
    RefundRequest = 1
}

public enum EscrowCaseStatus
{
    Open = 0,

    // Taken back by the side that opened it, or closed because the client
    // released the milestone or the freelancer resigned.
    Withdrawn = 1,

    // Closed by the arbiter, or by the exit that opens when the arbiter
    // stays silent.
    Settled = 2
}

// The backend's copy of a case on an escrow contract. The contract is the
// truth: a row is created, closed or settled only by reading the chain, never
// because a client said so. After the response window the arbiter can settle
// the milestone between the two parties; nobody else can.
public sealed class EscrowCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EscrowId { get; set; }

    [JsonIgnore]
    public Escrow Escrow { get; set; } = null!;

    public Guid MilestoneId { get; set; }

    [JsonIgnore]
    public MileStone Milestone { get; set; } = null!;

    public EscrowCaseKind Kind { get; set; }

    // The block time the contract recorded, not the time we noticed it.
    public DateTimeOffset OpenedAt { get; set; }

    public EscrowCaseStatus Status { get; set; } = EscrowCaseStatus.Open;

    public DateTimeOffset? ClosedAt { get; set; }

    public decimal? PaidToFreelancer { get; set; }
    public decimal PlatformFeePaid { get; set; }

    public decimal? ReturnedToClient { get; set; }

    public bool EscrowEnded { get; set; }

    // The reviewer's reasons, written for the two parties.
    public string? ReviewNote { get; set; }

    public DateTimeOffset? ReviewNoteAt { get; set; }

    // The model's advice to the reviewer. It is advice only: the reviewer
    // signs the settlement from their own wallet.
    public string? AdviceJson { get; set; }

    public int? AdviceFreelancerSharePercent { get; set; }

    public decimal? AdviceConfidence { get; set; }

    public Guid? AdviceAiRunId { get; set; }

    public DateTimeOffset? AdviceAt { get; set; }

    public DateTimeOffset? AdviceStartedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } =
        DateTimeOffset.UtcNow;
}
