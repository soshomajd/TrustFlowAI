using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Enums;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class SubmissionConcurrencyTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStaleChangeRequestCannotReopenAPaidMilestone(bool settleByCase)
    {
        var gate = new MilestoneWriteGate();
        using var application = WithGate(gate);
        using var client = CreateClient(application);
        // A second stage keeps the project in progress after the first is
        // paid, so a project-status check alone cannot prevent reopening it.
        var scenario = await CreateSubmittedMilestoneAsync(client, milestoneCount: 2);
        if (settleByCase)
        {
            FakeEscrowChainDeployer.OpenCase(scenario.ContractAddress,
                OnChainCaseKind.RefundRequest, DateTimeOffset.UtcNow.AddDays(-15));
            await SyncAsync(client, scenario);
        }

        gate.Arm(scenario.MilestoneId);
        var stale = SendAsync(client, HttpMethod.Patch,
            MilestonePath(scenario) + "/request-changes", scenario.Client.Token,
            new { Reason = "This feedback arrived after the payment." });
        try
        {
            await gate.WaitForWriteAsync();
            if (settleByCase)
            {
                FakeEscrowChainDeployer.SettleMilestone(scenario.ContractAddress, 0,
                    TokenAmountConverter.ToBaseUnits(333.33m, FakeEscrowChainDeployer.TokenDecimals), false);
                await SyncAsync(client, scenario);
            }
            else
            {
                FakeEscrowChainDeployer.MarkMilestoneReleased(scenario.ContractAddress, 0);
                using var approved = await SendAsync(client, HttpMethod.Patch,
                    MilestonePath(scenario) + "/approve", scenario.Client.Token);
                Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            }
        }
        finally
        {
            gate.Release();
        }

        using var response = await stale;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await SyncAsync(client, scenario);
        await SyncAsync(client, scenario);

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var milestone = await db.Milestones.AsNoTracking().SingleAsync(item => item.Id == scenario.MilestoneId);
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == scenario.ProjectId);
        var delivery = await db.MilestoneSubmissions.AsNoTracking().Include(item => item.Attachments)
            .SingleAsync(item => item.MilestoneId == scenario.MilestoneId);
        var gross = settleByCase ? 333.33m : 900m;
        var fee = settleByCase ? 33.333m : 90m;
        Assert.Equal(settleByCase ? MileStoneStatus.Settled : MileStoneStatus.Approved, milestone.Status);
        Assert.Equal(gross, milestone.PaidAmount);
        Assert.Equal(fee, milestone.PlatformFeePaid);
        Assert.Equal(gross, escrow.ReleasedAmount);
        Assert.Equal(fee, escrow.PlatformFeesPaid);
        Assert.Null(delivery.ClientFeedback);
        Assert.Null(delivery.ReviewedAt);
        Assert.Single(delivery.Attachments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStaleSubmissionCannotOverwriteSettlementOrResignation(bool settleByCase)
    {
        var gate = new MilestoneWriteGate();
        using var application = WithGate(gate);
        using var client = CreateClient(application);
        var scenario = await CreateSubmittedMilestoneAsync(client, milestoneCount: 2);
        const string originalFeedback = "Please correct the layout before submitting again.";
        using (var rejected = await SendAsync(client, HttpMethod.Patch,
                   MilestonePath(scenario) + "/request-changes", scenario.Client.Token,
                   new { Reason = originalFeedback }))
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        using (var started = await SendAsync(client, HttpMethod.Patch,
                   MilestonePath(scenario) + "/start", scenario.Freelancer.Token))
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        gate.Arm(scenario.MilestoneId);
        using var request = Request(HttpMethod.Post, MilestonePath(scenario) + "/submissions", scenario.Freelancer.Token);
        request.Content = new MultipartFormDataContent
        {
            { new StringContent("A new version that must not reopen the stage."), "note" },
            { new StringContent("https://example.test/updated-delivery"), "previewLink" }
        };
        var stale = client.SendAsync(request);
        try
        {
            await gate.WaitForWriteAsync();
            if (settleByCase)
                FakeEscrowChainDeployer.SettleMilestone(scenario.ContractAddress, 0,
                    TokenAmountConverter.ToBaseUnits(333.33m, FakeEscrowChainDeployer.TokenDecimals), false);
            else
                FakeEscrowChainDeployer.Resign(scenario.ContractAddress);
            await SyncAsync(client, scenario);
        }
        finally
        {
            gate.Release();
        }

        using var response = await stale;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await SyncAsync(client, scenario);
        await SyncAsync(client, scenario);

        using var scope = application.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var milestone = await db.Milestones.AsNoTracking().SingleAsync(item => item.Id == scenario.MilestoneId);
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == scenario.ProjectId);
        var delivery = await db.MilestoneSubmissions.AsNoTracking().Include(item => item.Attachments)
            .SingleAsync(item => item.MilestoneId == scenario.MilestoneId);
        Assert.Equal(settleByCase ? MileStoneStatus.Settled : MileStoneStatus.Pending, milestone.Status);
        if (settleByCase)
        {
            Assert.Equal(scenario.Freelancer.Id, milestone.FreelancerId);
            Assert.Equal(333.33m, milestone.PaidAmount);
            Assert.Equal(33.333m, milestone.PlatformFeePaid);
            Assert.Equal(333.33m, escrow.ReleasedAmount);
            Assert.Equal(33.333m, escrow.PlatformFeesPaid);
        }
        else
        {
            Assert.Null(milestone.FreelancerId);
            Assert.Null(milestone.PaidAmount);
            Assert.Equal(EscrowStatus.Terminated, escrow.Status);
            Assert.Equal(0m, escrow.ReleasedAmount);
            Assert.Equal(0m, escrow.PlatformFeesPaid);
        }
        Assert.Equal(1, delivery.Version);
        Assert.Equal(originalFeedback, delivery.ClientFeedback);
        Assert.NotNull(delivery.ReviewedAt);
        Assert.Single(delivery.Attachments);
    }

    private WebApplicationFactory<Program> WithGate(MilestoneWriteGate gate) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<AppDbContext>((_, options) => options.AddInterceptors(gate))));

    private static HttpClient CreateClient(WebApplicationFactory<Program> application) =>
        application.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });

    private static string MilestonePath(SubmittedMilestone scenario) =>
        $"/api/projects/{scenario.ProjectId}/milestones/{scenario.MilestoneId}";

    private static async Task SyncAsync(HttpClient client, SubmittedMilestone scenario)
    {
        using var response = await SendAsync(client, HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/escrow/sync?freelancerId={scenario.Freelancer.Id}", scenario.Client.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Stop the selected request just before its first milestone write. The
    // independent payment/resignation can then commit before it resumes.
    // Both ExecuteUpdate and SaveChanges use this gate, so the regressions
    // expose the old unconditional EF write as well as test the new claim.
    private sealed class MilestoneWriteGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid? _milestoneId;
        private int _paused;

        public void Arm(Guid milestoneId) => _milestoneId = milestoneId;
        public Task WaitForWriteAsync() => _reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await PauseAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await PauseAsync(command, cancellationToken);
            return result;
        }

        private async Task PauseAsync(DbCommand command, CancellationToken cancellationToken)
        {
            if (_milestoneId is not { } milestoneId ||
                !command.CommandText.Contains("UPDATE \"Milestones\"", StringComparison.Ordinal) ||
                !command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid value && value == milestoneId) ||
                Interlocked.CompareExchange(ref _paused, 1, 0) != 0) return;
            _reached.TrySetResult();
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }
}
