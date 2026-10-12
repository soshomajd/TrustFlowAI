namespace TrustFlow.Api.Models.Enums;

// What a call to the model was for. Every call goes through one gateway and
// is recorded under one of these, so quotas and audits work per feature.
public enum AiPurpose
{
    ProjectDraft = 0,
    MilestoneReview = 1,
    FreelancerProfileCoach = 2,
    FreelancerResumeDraft = 3,
    DisputeRuling = 4,

    // Reserved for a later version in which the model does a freelancer's
    // work from their own prompt. Nothing calls it yet; it exists so the
    // gateway, quota and audit trail do not need to change when it does.
    FreelancerWork = 5
}

public enum AiRunStatus
{
    Succeeded = 0,
    Failed = 1
}

public enum DisputeStatus
{
    // Waiting for statements and for a ruling to be requested.
    Open = 0,

    // The model reached a verdict. It is advice: it moves no funds.
    Ruled = 1,

    // The model could not decide on the evidence. Nothing was concluded.
    Inconclusive = 2,

    // Closed because the milestone was approved while it was still open:
    // the parties settled it themselves.
    Settled = 3
}

public enum DisputeVerdict
{
    FavorClient = 0,
    FavorFreelancer = 1,
    Split = 2
}

public enum MilestoneReviewRecommendation
{
    Approve = 0,
    RequestChanges = 1,
    InsufficientEvidence = 2
}
