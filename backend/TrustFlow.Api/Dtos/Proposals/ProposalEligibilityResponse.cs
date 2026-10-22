namespace TrustFlow.Api.Dtos.Proposals;

public sealed record ProposalEligibilityResponse(bool CanSubmit, string? Reason, string? Message);
