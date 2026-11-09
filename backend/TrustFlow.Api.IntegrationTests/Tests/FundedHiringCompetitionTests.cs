using System.Net;
using System.Text.Json;
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

public sealed class FundedHiringCompetitionTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient http = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });

    [Fact]
    public async Task MenuReservationKeepsTheCompetitorPending_ConfirmedFundingRejectsItOnce()
    {
        var scenario = await PrepareAsync();
        var loserBefore = await SnapshotAsync(scenario.LoserProposal);
        var escrow = await SelectAndDeployAsync(scenario);
        await AssertStatusAsync(scenario.WinnerProposal, ProposalStatus.AwaitingFunding);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        using (var competingSelection = await SendAsync(http, HttpMethod.Patch,
                   ProposalPath(scenario, scenario.LoserProposal) + "/accept", scenario.Client.Token))
            Assert.Equal(HttpStatusCode.Conflict, competingSelection.StatusCode);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);

        var stage = await StageAsync(escrow.Id, 0);
        Fund(escrow, stage);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.WinnerProposal, ProposalStatus.Funded);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        await AssertUnchangedProposalAsync(loserBefore);
        using (var clientList = await SendAsync(http, HttpMethod.Get,
                   $"/api/projects/{scenario.ProjectId}/proposals", scenario.Client.Token))
        {
            Assert.Equal(HttpStatusCode.OK, clientList.StatusCode);
            var rows = (await ReadAsync(clientList)).GetProperty("items").EnumerateArray();
            Assert.Equal("Rejected", rows.Single(item => item.GetProperty("id").GetGuid() == scenario.LoserProposal)
                .GetProperty("status").GetString());
        }
        using (var freelancerList = await SendAsync(http, HttpMethod.Get, "/api/proposals/mine", scenario.Loser.Token))
        {
            Assert.Equal(HttpStatusCode.OK, freelancerList.StatusCode);
            var row = Assert.Single((await ReadAsync(freelancerList)).GetProperty("items").EnumerateArray());
            Assert.Equal("Rejected", row.GetProperty("status").GetString());
        }

        var funded = await StageAsync(escrow.Id, 0);
        for (var repeat = 0; repeat < 2; repeat++) await SyncAsync(scenario);
        var after = await StageAsync(escrow.Id, 0);
        Assert.Equal(funded.FundedAt, after.FundedAt);
        Assert.Equal(funded.FundingTransactionHash, after.FundingTransactionHash);
        await AssertUnchangedProposalAsync(loserBefore);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.WinnerProposal, ProposalStatus.Accepted);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Escrows.CountAsync(item => item.ProjectId == scenario.ProjectId));
        Assert.Equal(1, await db.EscrowFundingStages.CountAsync(item => item.EscrowId == escrow.Id));
        Assert.Equal(0m, (await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id)).ReleasedAmount);
    }

    [Fact]
    public async Task ACompetingBundleIsRejectedWhole_AndDisjointScopeAndHistoricalTermsStayIntact()
    {
        var scenario = await PrepareAsync();
        var bundledAuthor = await NewPersonAsync(http, "Freelancer", "Menu and contact bidder");
        var disjointAuthor = await NewPersonAsync(http, "Freelancer", "Home page bidder");
        var bundled = await CreateProposalAsync(scenario.ProjectId, bundledAuthor,
            scenario.Milestones[1], scenario.Milestones[2]);
        var disjoint = await CreateProposalAsync(scenario.ProjectId, disjointAuthor, scenario.Milestones[0]);
        var bundledBefore = await SnapshotAsync(bundled);
        var disjointBefore = await SnapshotAsync(disjoint);
        Assert.Equal(200m, bundledBefore.BidAmount);
        var escrow = await SelectAndDeployAsync(scenario);
        Fund(escrow, await StageAsync(escrow.Id, 0));
        await SyncAsync(scenario);

        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        await AssertStatusAsync(bundled, ProposalStatus.Rejected);
        await AssertStatusAsync(disjoint, ProposalStatus.Pending);
        await AssertUnchangedProposalAsync(bundledBefore);
        await AssertUnchangedProposalAsync(disjointBefore);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stages = await db.Milestones.AsNoTracking().Where(item => item.ProjectId == scenario.ProjectId).ToListAsync();
        Assert.Equal(scenario.Winner.Id, stages.Single(item => item.Id == scenario.Milestones[1]).FreelancerId);
        Assert.All(stages.Where(item => item.Id != scenario.Milestones[1]), item => Assert.Null(item.FreelancerId));
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("terms")]
    [InlineData("amount")]
    [InlineData("signer")]
    [InlineData("receipt")]
    [InlineData("invalid-start")]
    public async Task InvalidFundingOrARejectedReconciliationCannotRejectThePendingCompetitor(string fault)
    {
        var scenario = await PrepareAsync();
        var escrow = await SelectAndDeployAsync(scenario);
        var stage = await StageAsync(escrow.Id, 0);
        Fund(escrow, stage);
        using var scope = factory.Services.CreateScope();
        var chain = scope.ServiceProvider.GetRequiredService<IEscrowChainDeployer>();
        var evidence = (await chain.GetStageEventsAsync(escrow.ContractAddress!, "", CancellationToken.None)).ToList();
        var funding = Assert.Single(evidence);
        evidence[0] = fault switch
        {
            "reference" => funding with { FundingReference = "0x" + new string('1', 64) },
            "terms" => funding with { TermsHash = "0x" + new string('2', 64) },
            "amount" => funding with { Amount = 1 },
            "signer" => funding with { Sender = escrow.FreelancerWalletAddress! },
            "receipt" => funding with { TransactionHash = "" },
            _ => funding
        };
        if (fault == "invalid-start")
            evidence.Add(funding with { Kind = "Start", Amount = 0, BlockNumber = funding.BlockNumber + 1,
                Sender = escrow.ClientWalletAddress! });
        FakeEscrowChainDeployer.SetStageEvents(escrow.ContractAddress!, evidence);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEscrowChainSyncOrchestrator>()
                .TrySyncAsync(escrow.Id, CancellationToken.None));
        await AssertStatusAsync(scenario.WinnerProposal, ProposalStatus.AwaitingFunding);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        Assert.Null((await StageAsync(escrow.Id, 0)).FundedAt);
        Assert.Null((await StageAsync(escrow.Id, 0)).FundingTransactionHash);
    }

    [Fact]
    public async Task MissingCanonicalDepositDoesNotRejectACompetitorEvenAfterRepeatedSync()
    {
        var scenario = await PrepareAsync();
        var escrow = await SelectAndDeployAsync(scenario);
        // A wallet rejection or reverted transaction emits no canonical
        // deposit. Reconciliation must not treat a reservation as a hire.
        for (var attempt = 0; attempt < 3; attempt++) await SyncAsync(scenario);
        await AssertStatusAsync(scenario.WinnerProposal, ProposalStatus.AwaitingFunding);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        var stage = await StageAsync(escrow.Id, 0);
        Assert.Null(stage.FundedAt);
        Assert.Null(stage.FundingTransactionHash);
    }

    [Fact]
    public async Task ExistingFundingReceiptRepairsAMissedCompetitorRejectionWithoutCreditingOrRewritingFunding()
    {
        var scenario = await PrepareAsync();
        var escrow = await SelectAndDeployAsync(scenario);
        Fund(escrow, await StageAsync(escrow.Id, 0));
        await SyncAsync(scenario);
        var saved = await StageAsync(escrow.Id, 0);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Models the previously persisted receipt with the old missing
            // competitor side effect; no chain event or amount is invented.
            await db.Proposals.Where(item => item.Id == scenario.LoserProposal)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, ProposalStatus.Pending));
        }
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        await SyncAsync(scenario);
        var recovered = await StageAsync(escrow.Id, 0);
        Assert.Equal(saved.FundedAt, recovered.FundedAt);
        Assert.Equal(saved.FundingTransactionHash, recovered.FundingTransactionHash);
        using var check = factory.Services.CreateScope();
        var dbAfter = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await dbAfter.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id);
        Assert.Equal(100m, after.TotalAmount);
        Assert.Equal(0m, after.ReleasedAmount);
        Assert.Equal(0m, after.PlatformFeesPaid);
        Assert.Equal(1, await dbAfter.EscrowFundingStages.CountAsync(item => item.EscrowId == escrow.Id));
    }

    [Fact]
    public async Task CancellationBeforeFirstFundingSyncDoesNotRejectThePendingCompetitor()
    {
        var scenario = await PrepareAsync();
        var escrow = await SelectAndDeployAsync(scenario);
        Fund(escrow, await StageAsync(escrow.Id, 0));
        FakeEscrowChainDeployer.MarkCancelled(escrow.ContractAddress!);
        await SyncAsync(scenario);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(EscrowStatus.Cancelled, (await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id)).Status);
        Assert.Null((await db.Milestones.AsNoTracking().SingleAsync(item => item.Id == scenario.Milestones[1])).FreelancerId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HistoricalFundingAfterCancellationOrResignationDoesNotRejectANewProposal(bool resign)
    {
        var scenario = await PrepareAsync();
        var escrow = await SelectAndDeployAsync(scenario);
        var stage = await StageAsync(escrow.Id, 0);
        Fund(escrow, stage);
        if (resign) FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        if (resign) FakeEscrowChainDeployer.Resign(escrow.ContractAddress!);
        else FakeEscrowChainDeployer.MarkCancelled(escrow.ContractAddress!);
        await SyncAsync(scenario);
        var newProposal = await CreateProposalAsync(scenario.ProjectId, scenario.Loser, scenario.Milestones[1]);
        for (var retry = 0; retry < 2; retry++) await SyncAsync(scenario);
        await AssertStatusAsync(newProposal, ProposalStatus.Pending);
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(resign ? EscrowStatus.Terminated : EscrowStatus.Cancelled,
            (await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id)).Status);
    }

    [Fact]
    public async Task AReservedButUnfundedPartOfTheWinnersBundleDoesNotRejectItsCompetitorUntilAppendedFunding()
    {
        var scenario = await PrepareAsync(winnerClaimsHomeAndMenu: true);
        var escrow = await SelectAndDeployAsync(scenario);
        var first = await StageAsync(escrow.Id, 0);
        Assert.Equal(scenario.Milestones[0], first.MilestoneId);
        Fund(escrow, first);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, first.FundingReference, first.TermsHash);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        FakeEscrowChainDeployer.MarkMilestoneReleased(escrow.ContractAddress!, 0);
        await SyncAsync(scenario);
        using (var prepareNext = await SendAsync(http, HttpMethod.Post,
                   $"/api/projects/{scenario.ProjectId}/escrow/stages", scenario.Client.Token,
                   new { MilestoneId = scenario.Milestones[1], FreelancerId = scenario.Winner.Id }))
            Assert.Equal(HttpStatusCode.OK, prepareNext.StatusCode);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Pending);
        var next = await StageAsync(escrow.Id, 1);
        Fund(escrow, next);
        await SyncAsync(scenario);
        await AssertStatusAsync(scenario.LoserProposal, ProposalStatus.Rejected);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Escrows.CountAsync(item => item.ProjectId == scenario.ProjectId));
        Assert.Equal(200m, (await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrow.Id)).TotalAmount);
    }

    private async Task<Scenario> PrepareAsync(bool winnerClaimsHomeAndMenu = false)
    {
        var client = await NewPersonAsync(http, "Client", "Menu project client");
        var winner = await NewPersonAsync(http, "Freelancer", "Selected menu freelancer");
        var loser = await NewPersonAsync(http, "Freelancer", "Competing menu freelancer");
        await ConnectWalletAsync(http, client.Token);
        await ConnectWalletAsync(http, winner.Token);
        await ConnectWalletAsync(http, loser.Token);
        var project = await ReadAsync(await SendAsync(http, HttpMethod.Post, "/api/projects", client.Token,
            new { Title = "Two menu proposals", Description = "Competition must follow verified milestone funding.",
                Budget = 300, Deadline = DateTimeOffset.UtcNow.AddDays(90) }));
        var projectId = project.GetProperty("id").GetGuid();
        var milestones = new List<Guid>();
        var titles = new[] { "Home page", "Menu page", "Contact page" };
        for (var sequence = 1; sequence <= titles.Length; sequence++)
        {
            using var response = await SendAsync(http, HttpMethod.Post, $"/api/projects/{projectId}/milestones", client.Token,
                new { Title = titles[sequence - 1], Description = "Deliver the agreed page.", Amount = 100,
                    SequenceNumber = sequence, Deadline = DateTimeOffset.UtcNow.AddDays(30 + sequence) });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            milestones.Add((await ReadAsync(response)).GetProperty("id").GetGuid());
        }
        var winnerStages = winnerClaimsHomeAndMenu ? milestones.Take(2).ToArray() : new[] { milestones[1] };
        var winnerProposal = await CreateProposalAsync(projectId, winner, winnerStages);
        var loserProposal = await CreateProposalAsync(projectId, loser, milestones[1]);
        return new Scenario(client, winner, loser, projectId, milestones.ToArray(), winnerProposal, loserProposal);
    }

    private async Task<Guid> CreateProposalAsync(Guid projectId, Person freelancer, params Guid[] milestones)
    {
        using var response = await SendAsync(http, HttpMethod.Post, $"/api/projects/{projectId}/proposals", freelancer.Token,
            new { CoverLetter = "I will complete the selected pages and revisions.", EstimatedDays = 3, MilestoneIds = milestones });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("id").GetGuid();
    }

    private async Task<Escrow> SelectAndDeployAsync(Scenario scenario)
    {
        using (var selected = await SendAsync(http, HttpMethod.Patch,
                   ProposalPath(scenario, scenario.WinnerProposal) + "/accept", scenario.Client.Token))
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using (var deployed = await SendAsync(http, HttpMethod.Post,
                   $"/api/projects/{scenario.ProjectId}/escrow/deploy", scenario.Client.Token))
            Assert.Equal(HttpStatusCode.OK, deployed.StatusCode);
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Escrows.AsNoTracking()
            .SingleAsync(item => item.ProjectId == scenario.ProjectId && item.FreelancerId == scenario.Winner.Id);
    }

    private async Task<EscrowFundingStage> StageAsync(Guid escrowId, int index)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().EscrowFundingStages.AsNoTracking()
            .SingleAsync(item => item.EscrowId == escrowId && item.ChainIndex == index);
    }

    private static void Fund(Escrow escrow, EscrowFundingStage stage) => FakeEscrowChainDeployer.FundStage(
        escrow.ContractAddress!, stage.ChainIndex!.Value, stage.FundingReference, stage.TermsHash, 100_000_000);

    private async Task SyncAsync(Scenario scenario)
    {
        using var response = await SendAsync(http, HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/escrow/sync?freelancerId={scenario.Winner.Id}", scenario.Client.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task AssertStatusAsync(Guid proposalId, ProposalStatus expected)
    {
        using var scope = factory.Services.CreateScope();
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Proposals.AsNoTracking()
            .Where(item => item.Id == proposalId).Select(item => item.Status).SingleAsync());
    }

    private async Task<ProposalSnapshot> SnapshotAsync(Guid proposalId)
    {
        using var scope = factory.Services.CreateScope();
        var proposal = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Proposals.AsNoTracking()
            .Include(item => item.ProposalMilestones).SingleAsync(item => item.Id == proposalId);
        return new ProposalSnapshot(proposal.Id, proposal.BidAmount, proposal.CoverLetter, proposal.EstimatedDays,
            proposal.CreatedAt, proposal.ProposalMilestones.Select(item => item.MilestoneId).Order().ToArray());
    }

    private async Task AssertUnchangedProposalAsync(ProposalSnapshot before)
    {
        var after = await SnapshotAsync(before.Id);
        Assert.Equal(before.BidAmount, after.BidAmount);
        Assert.Equal(before.CoverLetter, after.CoverLetter);
        Assert.Equal(before.EstimatedDays, after.EstimatedDays);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.Milestones, after.Milestones);
    }

    private static string ProposalPath(Scenario scenario, Guid proposalId) =>
        $"/api/projects/{scenario.ProjectId}/proposals/{proposalId}";
    private sealed record ProposalSnapshot(Guid Id, decimal BidAmount, string CoverLetter, int EstimatedDays,
        DateTimeOffset CreatedAt, Guid[] Milestones);
    private sealed record Scenario(Person Client, Person Winner, Person Loser, Guid ProjectId, Guid[] Milestones,
        Guid WinnerProposal, Guid LoserProposal);
}
