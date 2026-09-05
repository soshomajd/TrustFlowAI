namespace TrustFlow.Api.Models.Enums;

public enum EscrowStatus
{
    PendingDeployment,
    Deploying,
    AwaitingFunding,
    Funded,
    Completed,
    Cancelled
}