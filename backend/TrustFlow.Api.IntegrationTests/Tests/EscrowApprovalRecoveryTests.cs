using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Enums;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class EscrowApprovalRecoveryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EscrowApprovalRecoveryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    [Theory]
    [InlineData(1, "Completed", 1000, 100)]
    [InlineData(2, "InProgress", 900, 90)]
    public async Task LostApprovalResponse_RetryReturnsRecordedResultWithoutCreditingAgain(
        int milestoneCount, string projectStatus, int gross, int fee)
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client, milestoneCount: milestoneCount);
        FakeEscrowChainDeployer.MarkMilestoneReleased(scenario.ContractAddress, 0);
        var path = $"/api/projects/{scenario.ProjectId}/milestones/{scenario.MilestoneId}/approve";

        // The first response is discarded, as if the connection failed after commit.
        using var first = await SendAsync(_client, HttpMethod.Patch, path, scenario.Client.Token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == scenario.ProjectId);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var retry = await SendAsync(_client, HttpMethod.Patch, path, scenario.Client.Token);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            var result = await ReadAsync(retry);
            Assert.Equal("Approved", result.GetProperty("status").GetString());
            Assert.Equal(projectStatus, result.GetProperty("projectStatus").GetString());
            Assert.Equal(scenario.MilestoneId, result.GetProperty("id").GetGuid());
        }

        var after = await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id);
        Assert.Equal(gross, after.ReleasedAmount);
        Assert.Equal(fee, after.PlatformFeesPaid);
        Assert.Equal(escrow.UpdatedAt, after.UpdatedAt);
        Assert.Equal(escrow.CompletedAt, after.CompletedAt);
        var milestone = await db.Milestones.AsNoTracking().SingleAsync(item => item.Id == scenario.MilestoneId);
        Assert.Equal(MileStoneStatus.Approved, milestone.Status);
        Assert.Equal(gross, milestone.PaidAmount);
        Assert.Equal(fee, milestone.PlatformFeePaid);
    }

    [Fact]
    public async Task ConcurrentApprovalRetries_CreditOneGrossAwardAndOneFee()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        FakeEscrowChainDeployer.MarkMilestoneReleased(scenario.ContractAddress, 0);
        var path = $"/api/projects/{scenario.ProjectId}/milestones/{scenario.MilestoneId}/approve";
        var responses = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => SendAsync(_client, HttpMethod.Patch, path, scenario.Client.Token)));
        foreach (var response in responses)
        {
            using (response) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == scenario.ProjectId);
        Assert.Equal(1000m, escrow.ReleasedAmount);
        Assert.Equal(100m, escrow.PlatformFeesPaid);
        Assert.Equal(EscrowStatus.Completed, escrow.Status);
    }

    [Fact]
    public async Task RecordedApproval_IsStillRestrictedToItsOwningClient()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        FakeEscrowChainDeployer.MarkMilestoneReleased(scenario.ContractAddress, 0);
        var path = $"/api/projects/{scenario.ProjectId}/milestones/{scenario.MilestoneId}/approve";
        using var approved = await SendAsync(_client, HttpMethod.Patch, path, scenario.Client.Token);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var stranger = await NewPersonAsync(_client, "Client", "Other client");
        using var denied = await SendAsync(_client, HttpMethod.Patch, path, stranger.Token);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        using var wrongRole = await SendAsync(_client, HttpMethod.Patch, path, scenario.Freelancer.Token);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);
    }
}
