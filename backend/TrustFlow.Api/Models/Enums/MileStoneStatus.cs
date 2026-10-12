
namespace TrustFlow.Api.Models.Enums
{
    public enum MileStoneStatus
    {
        Pending = 0,
        InProgress = 1,
        Submitted = 2,
        Approved = 3,
        Rejected = 4,

        // Closed by the arbiter. The freelancer was paid PaidAmount, which
        // can be anything from nothing to the whole milestone.
        Settled = 5
    }
}