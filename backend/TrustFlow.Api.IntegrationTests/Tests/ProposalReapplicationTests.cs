using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ProposalReapplicationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ProposalReapplicationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedProposal_CanBeReplacedWithoutLosingItsRecord(bool rejected)
    {
        var (client, freelancer, project, stage) = await SetupAsync();
        await AssertEligibilityAsync(freelancer, project, true);
        var first = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await ReadAsync(first)).GetProperty("id").GetGuid();
        await AssertEligibilityAsync(freelancer, project, false, "ActiveProposal");
        var closed = await SendAsync(_client, HttpMethod.Patch,
            rejected ? $"/api/projects/{project}/proposals/{firstId}/reject" : $"/api/proposals/{firstId}/withdraw",
            rejected ? client.Token : freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        await AssertEligibilityAsync(freelancer, project, true);

        var second = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await ReadAsync(second)).GetProperty("id").GetGuid();
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(HttpStatusCode.Conflict, (await ApplyAsync(freelancer, project, stage)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Proposals.Where(item => item.ProjectId == project).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(rejected ? ProposalStatus.Rejected : ProposalStatus.Withdrawn,
            rows.Single(item => item.Id == firstId).Status);
        Assert.Equal(ProposalStatus.Pending, rows.Single(item => item.Id == secondId).Status);
    }

    [Theory]
    [InlineData(EscrowStatus.PendingDeployment)]
    [InlineData(EscrowStatus.Deploying)]
    [InlineData(EscrowStatus.AwaitingFunding)]
    [InlineData(EscrowStatus.Funded)]
    [InlineData(EscrowStatus.Completed)]
    [InlineData(EscrowStatus.Cancelled)]
    [InlineData(EscrowStatus.Terminated)]
    public async Task ExistingContract_CannotBeReplacedByReapplying(EscrowStatus status)
    {
        var (_, freelancer, project, stage) = await SetupAsync();
        var first = await ApplyAsync(freelancer, project, stage);
        var firstId = (await ReadAsync(first)).GetProperty("id").GetGuid();
        var withdrawn = await SendAsync(_client, HttpMethod.Patch,
            $"/api/proposals/{firstId}/withdraw", freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Escrows.Add(new Escrow
            {
                ProjectId = project, FreelancerId = freelancer.Id, ChainId = 31337,
                TokenAddress = "0x1111111111111111111111111111111111111111",
                TotalAmount = 100, Status = status
            });
            await db.SaveChangesAsync();
        }
        await AssertEligibilityAsync(freelancer, project, false, "ExistingEscrow");
        var other = await NewPersonAsync(_client, "Freelancer", "Unrelated Freelancer");
        // A different freelancer can still apply; the endpoint never accepts a target user ID.
        var unrelated = await SendAsync(_client, HttpMethod.Get,
            $"/api/projects/{project}/proposals/eligibility?freelancerId={freelancer.Id}", other.Token);
        Assert.Equal(HttpStatusCode.OK, unrelated.StatusCode);
        Assert.True((await ReadAsync(unrelated)).GetProperty("canSubmit").GetBoolean());
        var response = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("existing contract history", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Eligibility_RequiresFreelancer_AndDoesNotCachePersonalResults()
    {
        var (client, freelancer, project, _) = await SetupAsync();
        var url = $"/api/projects/{project}/proposals/eligibility";
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SendAsync(_client, HttpMethod.Get, url, client.Token)).StatusCode);
        var allowed = await SendAsync(_client, HttpMethod.Get, url, freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True(allowed.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(_client, HttpMethod.Get,
            $"/api/projects/{Guid.NewGuid()}/proposals/eligibility", freelancer.Token)).StatusCode);
    }

    [Theory]
    [InlineData(ProjectStatus.InProgress, true)]
    [InlineData(ProjectStatus.Completed, false)]
    [InlineData(ProjectStatus.Cancelled, false)]
    public async Task Eligibility_RespectsHiringStatus(ProjectStatus status, bool canSubmit)
    {
        var (_, freelancer, project, _) = await SetupAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Projects.SingleAsync(item => item.Id == project)).Status = status;
            await db.SaveChangesAsync();
        }
        await AssertEligibilityAsync(freelancer, project, canSubmit, canSubmit ? null : "NotHiring");
    }

    [Fact]
    public async Task Eligibility_BlocksAcceptedProposal_AndFullyAssignedOrIncompleteBudget()
    {
        var (client, freelancer, project, stage) = await SetupAsync();
        var first = await ApplyAsync(freelancer, project, stage);
        var proposalId = (await ReadAsync(first)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_client, HttpMethod.Patch,
            $"/api/projects/{project}/proposals/{proposalId}/accept", client.Token)).StatusCode);
        await AssertEligibilityAsync(freelancer, project, false, "ActiveProposal");
        var other = await NewPersonAsync(_client, "Freelancer", "Available Freelancer");
        await AssertEligibilityAsync(other, project, false, "NotHiring");

        var (_, freshFreelancer, freshProject, freshStage) = await SetupAsync();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Milestones.SingleAsync(item => item.Id == freshStage)).Amount = 99;
            await db.SaveChangesAsync();
        }
        await AssertEligibilityAsync(freshFreelancer, freshProject, false, "NotHiring");
    }

    private async Task AssertEligibilityAsync(Person freelancer, Guid project, bool canSubmit, string? reason = null)
    {
        var response = await SendAsync(_client, HttpMethod.Get,
            $"/api/projects/{project}/proposals/eligibility", freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(canSubmit, body.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(reason, body.GetProperty("reason").GetString());
        if (!canSubmit) Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task ConcurrentApplications_LeaveOnlyOneActiveProposal()
    {
        var (_, freelancer, project, stage) = await SetupAsync();
        var responses = await Task.WhenAll(
            ApplyAsync(freelancer, project, stage), ApplyAsync(freelancer, project, stage));
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, item => item.StatusCode == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Proposals.CountAsync(item => item.ProjectId == project));
    }

    private Task<HttpResponseMessage> ApplyAsync(Person freelancer, Guid project, Guid stage) =>
        SendAsync(_client, HttpMethod.Post, $"/api/projects/{project}/proposals", freelancer.Token,
            new { CoverLetter = "I can deliver these milestones.", EstimatedDays = 5, MilestoneIds = new[] { stage } });

    [Fact]
    public async Task ExactMoney_SurvivesDatabaseAndNestedMarketplaceWorkspaceProposalEscrowResponses()
    {
        var (client, freelancer, project, stage) = await SetupAsync();
        const decimal total = 9007199254740991.01m;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Projects.SingleAsync(item => item.Id == project)).Budget = total;
            (await db.Milestones.SingleAsync(item => item.Id == stage)).Amount = total;
            await db.SaveChangesAsync();
        }
        var marketplace = await ReadAsync(await _client.GetAsync($"/api/projects/{project}"));
        Assert.Equal("9007199254740991.01", marketplace.GetProperty("budgetExact").GetString());
        Assert.Equal("9007199254740991.01", marketplace.GetProperty("availableAmountExact").GetString());
        Assert.Equal("9007199254740991.01", marketplace.GetProperty("milestones")[0].GetProperty("amountExact").GetString());
        var proposal = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Created, proposal.StatusCode);
        Assert.Equal("9007199254740991.01", (await ReadAsync(proposal)).GetProperty("bidAmountExact").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Milestones.SingleAsync(item => item.Id == stage)).FreelancerId = freelancer.Id;
            db.Escrows.Add(new Escrow
            {
                ProjectId = project, FreelancerId = freelancer.Id, ChainId = 31337,
                TokenAddress = "0x1111111111111111111111111111111111111111",
                TotalAmount = total, ReleasedAmount = 1.000001m, PlatformFeesPaid = 0.100000m,
                Status = EscrowStatus.Funded
            });
            await db.SaveChangesAsync();
        }
        var workspace = await ReadAsync(await SendAsync(_client, HttpMethod.Get,
            $"/api/projects/{project}/workspace", client.Token));
        Assert.Equal("9007199254740991.01", workspace.GetProperty("budgetExact").GetString());
        var escrowResponse = await SendAsync(_client, HttpMethod.Get,
            $"/api/projects/{project}/escrow", freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, escrowResponse.StatusCode);
        var escrow = await ReadAsync(escrowResponse);
        Assert.Equal("9007199254740991.01", escrow.GetProperty("totalAmountExact").GetString());
        Assert.Equal(0.1m, decimal.Parse(escrow.GetProperty("platformFeesPaidExact").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(0.900001m, decimal.Parse(escrow.GetProperty("freelancerNetPaidExact").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<(Person Client, Person Freelancer, Guid Project, Guid Stage)> SetupAsync()
    {
        var client = await NewPersonAsync(_client, "Client", "Reapplication Client");
        var freelancer = await NewPersonAsync(_client, "Freelancer", "Reapplication Freelancer");
        var created = await SendAsync(_client, HttpMethod.Post, "/api/projects", client.Token, new
        {
            Title = "Proposal reapplication test", Description = "One fixed price milestone.",
            Budget = 100, Deadline = DateTimeOffset.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = (await ReadAsync(created)).GetProperty("id").GetGuid();
        var stageResponse = await SendAsync(_client, HttpMethod.Post, $"/api/projects/{project}/milestones", client.Token, new
        {
            Title = "Stage", Description = "Deliver the work.", Amount = 100, SequenceNumber = 1,
            Deadline = DateTimeOffset.UtcNow.AddDays(20)
        });
        Assert.Equal(HttpStatusCode.Created, stageResponse.StatusCode);
        return (client, freelancer, project, (await ReadAsync(stageResponse)).GetProperty("id").GetGuid());
    }
}
