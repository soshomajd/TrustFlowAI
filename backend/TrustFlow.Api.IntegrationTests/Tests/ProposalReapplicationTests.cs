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
        var first = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstId = (await ReadAsync(first)).GetProperty("id").GetGuid();
        var closed = await SendAsync(_client, HttpMethod.Patch,
            rejected ? $"/api/projects/{project}/proposals/{firstId}/reject" : $"/api/proposals/{firstId}/withdraw",
            rejected ? client.Token : freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

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

    [Fact]
    public async Task ExistingCancelledContract_CannotBeReplacedByReapplying()
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
                TotalAmount = 100, Status = EscrowStatus.Cancelled
            });
            await db.SaveChangesAsync();
        }
        var response = await ApplyAsync(freelancer, project, stage);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("existing contract history", await response.Content.ReadAsStringAsync());
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
