using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class FundedHiringTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient http = factory.CreateClient(new WebApplicationFactoryClientOptions {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });

    private async Task<(Person Client, Person Freelancer, Guid Project, Guid[] Stages, Guid Proposal)> Prepare(bool verifyWallets = true)
    {
        var client = await NewPersonAsync(http, "Client", "Funded Client");
        var freelancer = await NewPersonAsync(http, "Freelancer", "Funded Freelancer");
        if (verifyWallets) { await ConnectWalletAsync(http, client.Token); await ConnectWalletAsync(http, freelancer.Token); }
        var project = (await ReadAsync(await SendAsync(http, HttpMethod.Post, "/api/projects", client.Token,
            new { Title = "Funded hiring test", Description = "Two independent funded stages", Budget = 200, Deadline = DateTimeOffset.UtcNow.AddDays(60) })))
            .GetProperty("id").GetGuid();
        var stages = new List<Guid>();
        for (var i = 1; i <= 2; i++)
            stages.Add((await ReadAsync(await SendAsync(http, HttpMethod.Post, $"/api/projects/{project}/milestones", client.Token,
                new { Title = $"Stage {i}", Description = "Deliver and review this stage", Amount = 100, SequenceNumber = i, Deadline = DateTimeOffset.UtcNow.AddDays(30) })))
                .GetProperty("id").GetGuid());
        var proposal = (await ReadAsync(await SendAsync(http, HttpMethod.Post, $"/api/projects/{project}/proposals", freelancer.Token,
            new { CoverLetter = "I can deliver the first stage.", EstimatedDays = 3, MilestoneIds = new[] { stages[0] } }))).GetProperty("id").GetGuid();
        return (client, freelancer, project, stages.ToArray(), proposal);
    }

    private async Task<Escrow> SelectAndDeploy(Person client, Guid project, Guid proposal)
    {
        var selected = await SendAsync(http, HttpMethod.Patch, $"/api/projects/{project}/proposals/{proposal}/accept", client.Token);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        Assert.True((await ReadAsync(selected)).GetProperty("requiresFunding").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Post, $"/api/projects/{project}/escrow/deploy", client.Token)).StatusCode);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Escrows.AsNoTracking().SingleAsync(e => e.ProjectId == project);
    }

    private async Task<EscrowFundingStage> Stage(Guid escrow, int index)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EscrowFundingStages.AsNoTracking()
            .SingleAsync(s => s.EscrowId == escrow && s.ChainIndex == index);
    }

    private async Task Sync(Guid project, Person client)
        => Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Post, $"/api/projects/{project}/escrow/sync", client.Token)).StatusCode);

    private async Task FundAndStart(Escrow escrow, Person client, bool start = true)
    {
        var stage = await Stage(escrow.Id, 0);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash, 100_000_000);
        if (start) FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        await Sync(escrow.ProjectId, client);
    }

    [Fact]
    public async Task ReleaseThenAppendAndPartialArbitration_ConserveAmountsAndReuseTheContract()
    {
        var s = await Prepare(); var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        await FundAndStart(escrow, s.Client);
        FakeEscrowChainDeployer.MarkMilestoneReleased(escrow.ContractAddress!, 0);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        var nextUrl = $"/api/projects/{s.Project}/escrow/stages";
        for (var retry = 0; retry < 2; retry++)
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Post, nextUrl, s.Client.Token,
                new { MilestoneId = s.Stages[1], FreelancerId = s.Freelancer.Id })).StatusCode);
        var next = await Stage(escrow.Id, 1);
        Assert.Null(next.FundedAt);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/milestones/{s.Stages[1]}/start", s.Freelancer.Token)).StatusCode);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 1, next.FundingReference, next.TermsHash, 100_000_000);
        await Sync(s.Project, s.Client);
        Assert.Null((await Stage(escrow.Id, 1)).StartedAt);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 1, next.FundingReference, next.TermsHash);
        await Sync(s.Project, s.Client);
        FakeEscrowChainDeployer.SettleMilestone(escrow.ContractAddress!, 1, 20_000_000, false);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        using (var check = factory.Services.CreateScope())
        {
            var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
            var after = await db.Escrows.FindAsync(escrow.Id);
            Assert.Equal(200, after!.TotalAmount); Assert.Equal(120, after.ReleasedAmount);
            Assert.Equal(12, after.PlatformFeesPaid); Assert.Equal(EscrowStatus.Completed, after.Status);
            Assert.Equal(MileStoneStatus.Approved, (await db.Milestones.FindAsync(s.Stages[0]))!.Status);
            Assert.Equal(MileStoneStatus.Settled, (await db.Milestones.FindAsync(s.Stages[1]))!.Status);
            Assert.Equal(2, await db.EscrowExecutionRecords.CountAsync(e => e.EscrowId == escrow.Id));
        }
        // Add a genuinely new plan entry to the same contract, not just a pre-existing stage.
        var added = await SendAsync(http, HttpMethod.Post, nextUrl, s.Client.Token, new {
            MilestoneId = Guid.Empty, FreelancerId = s.Freelancer.Id, Title = "Additional work", Amount = 100,
            Deadline = DateTimeOffset.UtcNow.AddDays(40) });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var third = await Stage(escrow.Id, 2);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 2, third.FundingReference, third.TermsHash, 100_000_000);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 2, third.FundingReference, third.TermsHash);
        await Sync(s.Project, s.Client);
        using var final = factory.Services.CreateScope();
        var database = final.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await database.Escrows.CountAsync(e => e.ProjectId == s.Project));
        Assert.Equal(300, (await database.Escrows.FindAsync(escrow.Id))!.TotalAmount);
        Assert.Equal(300, (await database.Projects.FindAsync(s.Project))!.Budget);
        Assert.Equal(ProjectStatus.InProgress, (await database.Projects.FindAsync(s.Project))!.Status);
        Assert.Equal(MileStoneStatus.InProgress, (await database.Milestones.FindAsync(third.MilestoneId))!.Status);
    }

    [Fact]
    public async Task CancellationRefundsUnacceptedFundingAndRepeatedSyncDoesNotReassignIt()
    {
        var s = await Prepare(); var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        await FundAndStart(escrow, s.Client, start: false);
        FakeEscrowChainDeployer.MarkCancelled(escrow.ContractAddress!);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(EscrowStatus.Cancelled, (await db.Escrows.FindAsync(escrow.Id))!.Status);
        Assert.Equal(0, (await db.Escrows.FindAsync(escrow.Id))!.PlatformFeesPaid);
        Assert.Null((await db.Milestones.FindAsync(s.Stages[0]))!.FreelancerId);
        Assert.False((await Stage(escrow.Id, 0)).ReservationActive);
        Assert.Equal(ProposalStatus.Rejected, (await db.Proposals.FindAsync(s.Proposal))!.Status);
    }

    [Fact]
    public async Task HistoricalStartCannotReopenResignedWorkDuringLaterSync()
    {
        var s = await Prepare(); var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        await FundAndStart(escrow, s.Client);
        FakeEscrowChainDeployer.Resign(escrow.ContractAddress!);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(EscrowStatus.Terminated, (await db.Escrows.FindAsync(escrow.Id))!.Status);
        Assert.Null((await db.Milestones.FindAsync(s.Stages[0]))!.FreelancerId);
        Assert.Equal(MileStoneStatus.Pending, (await db.Milestones.FindAsync(s.Stages[0]))!.Status);
        Assert.NotNull((await Stage(escrow.Id, 0)).StartedAt);
    }

    [Fact]
    public async Task ConcurrentSelectionAndSyncKeepOneReservationAndNoDuplicateMoney()
    {
        var s = await Prepare();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/proposals/{s.Proposal}/accept", s.Client.Token)));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        var stage = await Stage(escrow.Id, 0);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash, 100_000_000);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Sync(s.Project, s.Client)));
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Escrows.CountAsync(e => e.ProjectId == s.Project));
        Assert.Equal(1, await db.EscrowFundingStages.CountAsync(e => e.EscrowId == escrow.Id));
        Assert.Equal(100, (await db.Escrows.FindAsync(escrow.Id))!.TotalAmount);
        Assert.Equal(0, (await db.Escrows.FindAsync(escrow.Id))!.ReleasedAmount);
    }

    [Fact]
    public async Task TwoFreelancersOnOneProjectHaveSeparateStageMapsAndPrivateFundingEvidence()
    {
        var s = await Prepare(); var first = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        var second = await NewPersonAsync(http, "Freelancer", "Second Payee"); await ConnectWalletAsync(http, second.Token);
        var proposal = (await ReadAsync(await SendAsync(http, HttpMethod.Post, $"/api/projects/{s.Project}/proposals", second.Token,
            new { CoverLetter = "I can deliver the second stage", EstimatedDays = 4, MilestoneIds = new[] { s.Stages[1] } }))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/proposals/{proposal}/accept", s.Client.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Post,
            $"/api/projects/{s.Project}/escrow/deploy?freelancerId={second.Id}", s.Client.Token)).StatusCode);
        using var check = factory.Services.CreateScope(); var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondEscrow = await db.Escrows.AsNoTracking().SingleAsync(e => e.ProjectId == s.Project && e.FreelancerId == second.Id);
        Assert.NotEqual(first.ContractAddress, secondEscrow.ContractAddress);
        Assert.NotEqual(first.FreelancerWalletAddress, secondEscrow.FreelancerWalletAddress);
        var stage = await Stage(first.Id, 0);
        FakeEscrowChainDeployer.FundStage(first.ContractAddress!, 0, stage.FundingReference, stage.TermsHash, 100_000_000);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Post,
            $"/api/projects/{s.Project}/escrow/sync?freelancerId={s.Freelancer.Id}", s.Client.Token)).StatusCode);
        Assert.NotNull((await Stage(first.Id, 0)).FundedAt);
        Assert.Null((await Stage(secondEscrow.Id, 0)).FundedAt);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(http, HttpMethod.Get,
            $"/api/projects/{s.Project}/escrow/stages?freelancerId={second.Id}", s.Freelancer.Token)).StatusCode);
    }

    [Fact]
    public async Task SelectionReservesOnly_FirstFundingThenFreelancerAcceptanceActivatesWork()
    {
        var s = await Prepare();
        var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        await Sync(s.Project, s.Client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(3, (await db.Projects.FindAsync(s.Project))!.HiringFlowVersion);
            Assert.Equal(ProjectStatus.Open, (await db.Projects.FindAsync(s.Project))!.Status);
            Assert.Equal(ProposalStatus.AwaitingFunding, (await db.Proposals.FindAsync(s.Proposal))!.Status);
            Assert.Equal(EscrowStatus.AwaitingFunding, (await db.Escrows.FindAsync(escrow.Id))!.Status);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/milestones/{s.Stages[0]}/start", s.Freelancer.Token)).StatusCode);
        var stage = await Stage(escrow.Id, 0);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash, 100_000_000);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        Assert.NotNull((await Stage(escrow.Id, 0)).FundedAt);
        Assert.Null((await Stage(escrow.Id, 0)).StartedAt);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        await Sync(s.Project, s.Client); await Sync(s.Project, s.Client);
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(ProposalStatus.Accepted, (await database.Proposals.FindAsync(s.Proposal))!.Status);
        Assert.Equal(ProjectStatus.InProgress, (await database.Projects.FindAsync(s.Project))!.Status);
        Assert.Equal(MileStoneStatus.InProgress, (await database.Milestones.FindAsync(s.Stages[0]))!.Status);
        Assert.Null((await database.Milestones.FindAsync(s.Stages[1]))!.FreelancerId);
        Assert.NotNull((await Stage(escrow.Id, 0)).StartedAt);
        Assert.Equal(100, (await database.Escrows.FindAsync(escrow.Id))!.TotalAmount);
        Assert.Equal(0, (await database.Escrows.FindAsync(escrow.Id))!.ReleasedAmount);
    }

    [Fact]
    public async Task MissingWalletsCannotReserveOrBypassFundingThroughTheOldCreationRoute()
    {
        var s = await Prepare(verifyWallets: false);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/proposals/{s.Proposal}/accept", s.Client.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(http, HttpMethod.Post,
            $"/api/projects/{s.Project}/escrow", s.Client.Token,
            new { ChainId = 31337, TokenAddress = "0x5FbDB2315678afecb367f032d93F642f64180aa3" })).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Escrows.AnyAsync(e => e.ProjectId == s.Project));
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("terms")]
    [InlineData("amount")]
    [InlineData("signer")]
    public async Task MismatchedDepositEvidenceCannotPromoteFunding(string fault)
    {
        var s = await Prepare(); var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        var stage = await Stage(escrow.Id, 0);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 0,
            fault == "reference" ? "0x" + new string('1', 64) : stage.FundingReference,
            fault == "terms" ? "0x" + new string('2', 64) : stage.TermsHash,
            fault == "amount" ? 1 : 100_000_000);
        using var scope = factory.Services.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IEscrowChainDeployer>();
        if (fault == "signer")
        {
            var evidence = await chain.GetStageEventsAsync(escrow.ContractAddress!, "", CancellationToken.None);
            FakeEscrowChainDeployer.SetStageEvents(escrow.ContractAddress!, evidence.Select(e => e with { Sender = escrow.FreelancerWalletAddress! }).ToList());
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<IEscrowChainSyncOrchestrator>()
            .TrySyncAsync(escrow.Id, CancellationToken.None));
        Assert.Null((await Stage(escrow.Id, 0)).FundedAt);
    }

    [Fact]
    public async Task ASelectedMilestoneCannotBeReservedForAnotherFreelancer_AndOnlyPartiesSeeItsTerms()
    {
        var s = await Prepare();
        var second = await NewPersonAsync(http, "Freelancer", "Other Freelancer"); await ConnectWalletAsync(http, second.Token);
        var proposal = (await ReadAsync(await SendAsync(http, HttpMethod.Post, $"/api/projects/{s.Project}/proposals", second.Token,
            new { CoverLetter = "A competing proposal", EstimatedDays = 3, MilestoneIds = new[] { s.Stages[0] } }))).GetProperty("id").GetGuid();
        var escrow = await SelectAndDeploy(s.Client, s.Project, s.Proposal);
        Assert.Equal(HttpStatusCode.Conflict, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/proposals/{proposal}/accept", s.Client.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(http, HttpMethod.Get,
            $"/api/projects/{s.Project}/escrow/stages?freelancerId={s.Freelancer.Id}", second.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(http, HttpMethod.Post,
            $"/api/projects/{s.Project}/escrow/stages", s.Freelancer.Token,
            new { MilestoneId = s.Stages[1], FreelancerId = s.Freelancer.Id })).StatusCode);
        // Repeated Client selection resumes the same reservation and contract.
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(http, HttpMethod.Patch,
            $"/api/projects/{s.Project}/proposals/{s.Proposal}/accept", s.Client.Token)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Escrows.CountAsync(e => e.ProjectId == s.Project));
        Assert.Equal(escrow.Id, (await Stage(escrow.Id, 0)).EscrowId);
    }
}
