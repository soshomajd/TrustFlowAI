namespace TrustFlow.Api.Models.Enums;

public enum EscrowStatus
{
    PendingDeployment,
    Deploying,
    AwaitingFunding,
    Funded,
    Completed,
    Cancelled,

    // Ended early after work started: the freelancer resigned or a settled
    // case ended it. Whatever was not paid out went back to the client.
    Terminated
}