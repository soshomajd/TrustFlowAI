using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Notifications;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.Controllers;

// Notifications are derived from current workflow state instead of being
// stored, so they can never disagree with the project, escrow and milestone
// records they describe, and no financial write path had to change to raise
// one. The cost is that there is no read/unread history.
[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(AppDbContext dbContext)
    : ControllerBase
{
    private const string ActionSeverity = "action";
    private const string WarningSeverity = "warning";
    private const string InfoSeverity = "info";

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            return Unauthorized();
        }

        var items = new List<NotificationItemResponse>();

        if (User.IsInRole(AppRoles.Client))
        {
            items.AddRange(
                await BuildClientItemsAsync(userId, cancellationToken)
            );
        }

        if (User.IsInRole(AppRoles.Freelancer))
        {
            items.AddRange(
                await BuildFreelancerItemsAsync(userId, cancellationToken)
            );
        }

        var ordered = items
            .OrderBy(item => item.Severity switch
            {
                ActionSeverity => 0,
                WarningSeverity => 1,
                _ => 2
            })
            .ThenBy(item => item.ProjectTitle, StringComparer.Ordinal)
            .ToList();

        return Ok(new NotificationsResponse
        {
            Items = ordered,

            // Only items the viewer can act on are counted, so the badge
            // means "waiting on you" rather than "unread".
            ActionCount = ordered.Count(item =>
                item.Severity != InfoSeverity)
        });
    }

    private async Task<List<NotificationItemResponse>>
        BuildClientItemsAsync(
            Guid clientId,
            CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.ClientId == clientId &&
                (project.Status == ProjectStatus.Open ||
                 project.Status == ProjectStatus.InProgress))
            .Select(project => new
            {
                project.Id,
                project.Title,
                project.Budget,
                project.Status,

                Allocated = project.Milestones
                    .Sum(milestone => (decimal?)milestone.Amount) ?? 0m,

                MilestoneCount = project.Milestones.Count,

                PendingProposalCount = project.Proposals
                    .Count(proposal =>
                        proposal.Status == ProposalStatus.Pending),

                SubmittedMilestones = project.Milestones
                    .Where(milestone =>
                        milestone.Status == MileStoneStatus.Submitted)
                    .OrderBy(milestone => milestone.SequenceNumber)
                    .Select(milestone => new
                    {
                        milestone.Id,
                        milestone.Title
                    })
                    .ToList(),

                // A project can now have one escrow per freelancer, so the
                // client is reminded while any of them still needs setting
                // up or funding.
                EscrowStatuses = project.Escrows
                    .Select(escrow => escrow.Status)
                    .ToList(),

                AssignedFreelancerCount = project.Milestones
                    .Where(milestone => milestone.FreelancerId != null)
                    .Select(milestone => milestone.FreelancerId)
                    .Distinct()
                    .Count()
            })
            .ToListAsync(cancellationToken);

        var items = new List<NotificationItemResponse>();

        foreach (var project in projects)
        {
            var href = $"/dashboard/client/projects/{project.Id}";

            if (project.Status == ProjectStatus.Open &&
                project.Allocated != project.Budget)
            {
                var remaining = project.Budget - project.Allocated;

                items.Add(new NotificationItemResponse
                {
                    Id = $"project-hidden:{project.Id}",
                    Kind = "ProjectHidden",
                    Severity = WarningSeverity,
                    Title = "Project is hidden from freelancers",

                    Message = project.MilestoneCount == 0
                        ? "Add milestones that add up to the budget so "
                          + "freelancers can find this project."
                        : FormatAmount(remaining)
                          + " of the budget is still unassigned, so "
                          + "freelancers cannot see this project yet.",

                    Href = href,
                    ProjectTitle = project.Title
                });
            }

            if (project.Status == ProjectStatus.Open &&
                project.PendingProposalCount > 0)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"proposals-waiting:{project.Id}",
                    Kind = "ProposalsWaiting",
                    Severity = ActionSeverity,

                    Title = project.PendingProposalCount == 1
                        ? "1 proposal is waiting"
                        : project.PendingProposalCount
                          + " proposals are waiting",

                    Message =
                        "Read each proposal and open the freelancer "
                        + "profile before you accept one.",

                    Href = href,
                    ProjectTitle = project.Title
                });
            }

            if (project.Status == ProjectStatus.InProgress)
            {
                // One escrow per assigned freelancer, so a missing one is
                // as much of a blocker as an undeployed one.
                var readyEscrowCount = project.EscrowStatuses
                    .Count(status =>
                        status != EscrowStatus.PendingDeployment &&
                        status != EscrowStatus.Deploying);

                var unfundedCount = project.EscrowStatuses
                    .Count(status =>
                        status == EscrowStatus.AwaitingFunding);

                if (readyEscrowCount < project.AssignedFreelancerCount)
                {
                    items.Add(new NotificationItemResponse
                    {
                        Id = $"escrow-setup:{project.Id}",
                        Kind = "EscrowSetupNeeded",
                        Severity = ActionSeverity,
                        Title = "Set up the secure payment",

                        Message =
                            "Each freelancer on this project needs their "
                            + "own escrow created and deployed before they "
                            + "can start.",

                        Href = href,
                        ProjectTitle = project.Title
                    });
                }
                else if (unfundedCount > 0)
                {
                    items.Add(new NotificationItemResponse
                    {
                        Id = $"escrow-funding:{project.Id}",
                        Kind = "EscrowFundingNeeded",
                        Severity = ActionSeverity,
                        Title = "Fund the escrow",

                        Message = unfundedCount == 1
                            ? "Approve the token and fund one freelancer's "
                              + "escrow so they can start."
                            : unfundedCount
                              + " escrows on this project are still waiting "
                              + "to be funded.",

                        Href = href,
                        ProjectTitle = project.Title
                    });
                }
            }

            foreach (var milestone in project.SubmittedMilestones)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"milestone-review:{milestone.Id}",
                    Kind = "MilestoneWaitingForReview",
                    Severity = ActionSeverity,
                    Title = "Delivered work is waiting for you",

                    Message =
                        milestone.Title
                        + " was submitted. Check it, then release payment "
                        + "or ask for changes.",

                    Href = href,
                    ProjectTitle = project.Title
                });
            }
        }

        return items;
    }

    private async Task<List<NotificationItemResponse>>
        BuildFreelancerItemsAsync(
            Guid freelancerId,
            CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects
            .AsNoTracking()
            .Where(project =>
                project.Milestones.Any(milestone =>
                    milestone.FreelancerId == freelancerId) &&
                project.Status == ProjectStatus.InProgress)
            .Select(project => new
            {
                project.Id,
                project.Title,

                EscrowStatus = project.Escrows
                    .Where(escrow =>
                        escrow.FreelancerId == freelancerId)
                    .Select(escrow => (EscrowStatus?)escrow.Status)
                    .FirstOrDefault(),

                Milestones = project.Milestones
                    .OrderBy(milestone => milestone.SequenceNumber)
                    .Select(milestone => new
                    {
                        milestone.Id,
                        milestone.Title,
                        milestone.Status
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = new List<NotificationItemResponse>();

        foreach (var project in projects)
        {
            var href = $"/dashboard/freelancer/projects/{project.Id}";

            if (project.EscrowStatus != EscrowStatus.Funded &&
                project.EscrowStatus != EscrowStatus.Completed)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"awaiting-funding:{project.Id}",
                    Kind = "WaitingForFunding",
                    Severity = InfoSeverity,
                    Title = "Waiting for the client to fund",

                    Message =
                        "Do not start yet. The escrow must say Funded "
                        + "before your payment is protected.",

                    Href = href,
                    ProjectTitle = project.Title
                });

                continue;
            }

            var rejected = project.Milestones
                .Where(milestone =>
                    milestone.Status == MileStoneStatus.Rejected)
                .ToList();

            foreach (var milestone in rejected)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"milestone-changes:{milestone.Id}",
                    Kind = "MilestoneChangesRequested",
                    Severity = ActionSeverity,
                    Title = "The client asked for changes",

                    Message =
                        "Read the feedback on " + milestone.Title
                        + " and send a new version.",

                    Href = href,
                    ProjectTitle = project.Title
                });
            }

            var inProgress = project.Milestones
                .FirstOrDefault(milestone =>
                    milestone.Status == MileStoneStatus.InProgress);

            if (inProgress is not null)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"milestone-submit:{inProgress.Id}",
                    Kind = "MilestoneWaitingForDelivery",
                    Severity = ActionSeverity,
                    Title = "Send your delivery",

                    Message =
                        inProgress.Title
                        + " is started. Submit your note, files or links "
                        + "when it is ready.",

                    Href = href,
                    ProjectTitle = project.Title
                });

                continue;
            }

            // Milestones run in order, so only the first pending one can be
            // started, and only once nothing is waiting to be fixed.
            var nextPending = project.Milestones
                .FirstOrDefault(milestone =>
                    milestone.Status == MileStoneStatus.Pending);

            if (nextPending is not null && rejected.Count == 0)
            {
                items.Add(new NotificationItemResponse
                {
                    Id = $"milestone-start:{nextPending.Id}",
                    Kind = "MilestoneReadyToStart",
                    Severity = ActionSeverity,
                    Title = "You can start the next milestone",

                    Message =
                        "The escrow is funded, so " + nextPending.Title
                        + " is ready to start.",

                    Href = href,
                    ProjectTitle = project.Title
                });
            }
        }

        return items;
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N2", CultureInfo.InvariantCulture);
}
