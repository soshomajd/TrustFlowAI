namespace TrustFlow.Api.Models;

// Reuses the persisted unique case identity: identical for both participants,
// available before signing, and unchanged by retries, refreshes or settlement.
public static class EscrowCaseReference
{
    public static string Format(Guid caseId) => $"TF-{caseId:N}".ToUpperInvariant();
}
