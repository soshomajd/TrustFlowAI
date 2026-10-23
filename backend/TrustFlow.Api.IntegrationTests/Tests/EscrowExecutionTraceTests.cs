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

public sealed class EscrowExecutionTraceTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });

    [Fact]
    public async Task ApprovalRequiresAndPersistsExactEvidence_AndConcurrentSyncDoesNotDuplicateOrCredit()
    {
        var s = await CreateSubmittedMilestoneAsync(client); FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        Guid escrowId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); escrowId = await db.Escrows.Where(item => item.ProjectId == s.ProjectId).Select(item => item.Id).SingleAsync();
            var expected = await scope.ServiceProvider.GetRequiredService<IEscrowChainDeployer>().GetExecutionEventsAsync(s.ContractAddress, "", CancellationToken.None);
            await Task.WhenAll(Enumerable.Range(0,4).Select(async _ => {
                using var concurrent = factory.Services.CreateScope();
                await concurrent.ServiceProvider.GetRequiredService<IEscrowExecutionTrace>().CaptureAsync(escrowId, CancellationToken.None);
            }));
            var saved = await db.EscrowExecutionRecords.Where(item => item.EscrowId == escrowId).ToListAsync();
            Assert.Equal(expected.Count, saved.Count);
            foreach (var item in expected)
            {
                var row = Assert.Single(saved, row => row.MilestoneIndex == item.MilestoneIndex);
                Assert.Equal(item.TransactionHash, row.TransactionHash); Assert.Equal(item.OccurredAt, row.OccurredAt);
                Assert.Equal(item.BlockHash, row.BlockHash); Assert.Equal(item.TransactionSender, row.TransactionSender);
                Assert.Equal(item.MilestoneIndex < 0 ? null : s.MilestoneId, row.MilestoneId);
            }
            Assert.Equal(0, (await db.Escrows.SingleAsync(item => item.Id == escrowId)).ReleasedAmount);
        }
        for(var retry=0;retry<2;retry++) Assert.Equal(HttpStatusCode.OK, (await SendAsync(client,HttpMethod.Patch,$"/api/projects/{s.ProjectId}/milestones/{s.MilestoneId}/approve",s.Client.Token)).StatusCode);
        using var check = factory.Services.CreateScope(); var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await database.Escrows.SingleAsync(item => item.Id == escrowId);
        Assert.Equal(1000, after.ReleasedAmount); Assert.Equal(100, after.PlatformFeesPaid);
        Assert.Equal(2, await database.EscrowExecutionRecords.CountAsync(item => item.EscrowId == escrowId));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("hash")]
    [InlineData("time")]
    [InlineData("amount")]
    [InlineData("index")]
    [InlineData("kind")]
    [InlineData("participant")]
    public async Task IncompleteOrMismatchedEvidenceCannotBecomeAnApprovedPayment(string fault)
    {
        var s = await CreateSubmittedMilestoneAsync(client); FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow=await db.Escrows.AsNoTracking().SingleAsync(item=>item.ProjectId==s.ProjectId);
        var events=(await scope.ServiceProvider.GetRequiredService<IEscrowChainDeployer>().GetExecutionEventsAsync(s.ContractAddress,"",CancellationToken.None)).ToList();
        var payment=events.Single(item=>item.MilestoneIndex==0);
        events.Remove(payment);
        if(fault!="missing") events.Add(fault switch {
            "hash"=>payment with {TransactionHash=""},"time"=>payment with {OccurredAt=DateTimeOffset.UnixEpoch},
            "amount"=>payment with {GrossPaid=1},"index"=>payment with {MilestoneIndex=4},
            "kind"=>payment with {Kind="CaseSettlement"},"participant"=>payment with {Participant="0x"+new string('9',40)},_=>payment});
        FakeEscrowChainDeployer.SetExecutionEvents(s.ContractAddress,events);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.ServiceProvider.GetRequiredService<IEscrowExecutionTrace>().CaptureAsync(escrow.Id,CancellationToken.None));
        Assert.Empty(await db.EscrowExecutionRecords.Where(item=>item.EscrowId==escrow.Id).ToListAsync());
        Assert.Equal(0,(await db.Escrows.SingleAsync(item=>item.Id==escrow.Id)).ReleasedAmount);
        Assert.Equal(MileStoneStatus.Submitted,(await db.Milestones.SingleAsync(item=>item.Id==s.MilestoneId)).Status);
    }

    [Fact]
    public async Task CompletedLegacyPaymentCanBackfillViaSyncWithoutChangingItsAccounting()
    {
        var s=await CreateSubmittedMilestoneAsync(client);FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress,0);
        Assert.Equal(HttpStatusCode.OK,(await SendAsync(client,HttpMethod.Patch,$"/api/projects/{s.ProjectId}/milestones/{s.MilestoneId}/approve",s.Client.Token)).StatusCode);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before=await db.Escrows.AsNoTracking().SingleAsync(item=>item.ProjectId==s.ProjectId);
        await db.EscrowExecutionRecords.Where(item=>item.EscrowId==before.Id).ExecuteDeleteAsync();
        var sync=scope.ServiceProvider.GetRequiredService<IEscrowChainSyncOrchestrator>();
        Assert.True(await sync.TrySyncAsync(before.Id,CancellationToken.None));
        Assert.False(await sync.TrySyncAsync(before.Id,CancellationToken.None));
        var after=await db.Escrows.AsNoTracking().SingleAsync(item=>item.Id==before.Id);
        Assert.Equal(before.Status,after.Status);Assert.Equal(before.ReleasedAmount,after.ReleasedAmount);Assert.Equal(before.PlatformFeesPaid,after.PlatformFeesPaid);Assert.Equal(before.CompletedAt,after.CompletedAt);
        Assert.Equal(2,await db.EscrowExecutionRecords.CountAsync(item=>item.EscrowId==before.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(400)]
    public async Task CaseSettlementEvidenceIncludesZeroAwardsAndDoesNotDoubleCount(int gross)
    {
        var s=await CreateSubmittedMilestoneAsync(client);
        FakeEscrowChainDeployer.SettleMilestone(s.ContractAddress,0,TokenAmountConverter.ToBaseUnits(gross,6),false);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow=await db.Escrows.AsNoTracking().SingleAsync(item=>item.ProjectId==s.ProjectId);
        var sync=scope.ServiceProvider.GetRequiredService<IEscrowChainSyncOrchestrator>();
        Assert.True(await sync.TrySyncAsync(escrow.Id,CancellationToken.None));await sync.TrySyncAsync(escrow.Id,CancellationToken.None);
        var evidence=await db.EscrowExecutionRecords.SingleAsync(item=>item.EscrowId==escrow.Id&&item.MilestoneIndex==0);
        Assert.Equal("CaseSettlement",evidence.Kind);Assert.Equal(s.MilestoneId,evidence.MilestoneId);Assert.True(evidence.OccurredAt>DateTimeOffset.UnixEpoch);
        var after=await db.Escrows.AsNoTracking().SingleAsync(item=>item.Id==escrow.Id);Assert.Equal(gross,after.ReleasedAmount);Assert.Equal(gross/10,after.PlatformFeesPaid);
    }
}
