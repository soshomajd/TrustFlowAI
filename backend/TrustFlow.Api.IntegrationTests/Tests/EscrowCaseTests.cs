using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

// The contract decides; these tests make the chain "say" things through the
// fake and check that the backend copies exactly that, shows it to the right
// people, and never lets anyone but the chain move a case along.
public sealed class EscrowCaseTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const decimal Delivered = 1000m;

    private readonly CustomWebApplicationFactory _factory;

    private readonly HttpClient _client;

    public EscrowCaseTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _factory.AiModel.Reset();

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    // ---- recording what the chain says ----------------------------------

    [Fact]
    public async Task ACaseIsRecordedOnlyOnceTheChainShowsIt()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        Assert.Empty(await ListCasesAsync(scenario, scenario.Client));

        // Nothing happened on the chain, so syncing records nothing.
        await SyncAsync(scenario, scenario.Freelancer);
        Assert.Empty(await ListCasesAsync(scenario, scenario.Client));

        var openedAt = DateTimeOffset.UtcNow.AddDays(-1);

        FakeEscrowChainDeployer.OpenCase(
            scenario.ContractAddress,
            OnChainCaseKind.PaymentRequest,
            openedAt);

        await SyncAsync(scenario, scenario.Freelancer);

        var cases = await ListCasesAsync(scenario, scenario.Client);
        var recorded = Assert.Single(cases);

        Assert.Equal("PaymentRequest", recorded.GetProperty("kind").GetString());
        Assert.Equal("Freelancer", recorded.GetProperty("openedByRole").GetString());
        Assert.Equal("Open", recorded.GetProperty("status").GetString());
        Assert.Equal(
            scenario.MilestoneId,
            recorded.GetProperty("milestoneId").GetGuid());

        // The 14 days run from the time the chain recorded, not from when
        // the backend noticed.
        Assert.Equal(
            openedAt.AddDays(14).ToUnixTimeSeconds(),
            recorded.GetProperty("settleableAt").GetDateTimeOffset()
                .ToUnixTimeSeconds());

        // Syncing again changes nothing.
        await SyncAsync(scenario, scenario.Client);
        Assert.Single(await ListCasesAsync(scenario, scenario.Client));
    }

    [Fact]
    public async Task OnlyTheTwoSidesOfTheProjectSeeItsCases()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        FakeEscrowChainDeployer.OpenCase(
            scenario.ContractAddress,
            OnChainCaseKind.RefundRequest,
            DateTimeOffset.UtcNow);

        await SyncAsync(scenario, scenario.Client);

        Assert.Single(await ListCasesAsync(scenario, scenario.Client));
        Assert.Single(await ListCasesAsync(scenario, scenario.Freelancer));

        var stranger = await NewPersonAsync(_client, "Freelancer", "Stranger");
        var otherClient = await NewPersonAsync(_client, "Client", "Other Client");

        Assert.Empty(await ListCasesAsync(scenario, stranger));
        Assert.Empty(await ListCasesAsync(scenario, otherClient));
    }

    [Fact]
    public async Task ACaseTakenBackIsClosed()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        FakeEscrowChainDeployer.OpenCase(
            scenario.ContractAddress,
            OnChainCaseKind.RefundRequest,
            DateTimeOffset.UtcNow);

        await SyncAsync(scenario, scenario.Client);

        FakeEscrowChainDeployer.WithdrawCase(scenario.ContractAddress);
        await SyncAsync(scenario, scenario.Client);

        var recorded = Assert.Single(
            await ListCasesAsync(scenario, scenario.Client));

        Assert.Equal("Withdrawn", recorded.GetProperty("status").GetString());
    }

    // ---- the arbiter settles --------------------------------------------

    [Fact]
    public async Task APaymentTheArbiterSettledInFullFinishesTheProject()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        await OpenCaseAsync(scenario, OnChainCaseKind.PaymentRequest, 15);

        FakeEscrowChainDeployer.SettleMilestone(
            scenario.ContractAddress, 0, Units(Delivered), endEscrow: false);

        await SyncAsync(scenario, scenario.Freelancer);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var milestone = await db.Milestones.SingleAsync(
            item => item.Id == scenario.MilestoneId);

        Assert.Equal(MileStoneStatus.Settled, milestone.Status);
        Assert.Equal(Delivered, milestone.PaidAmount);

        var escrow = await db.Escrows.SingleAsync(
            item => item.ProjectId == scenario.ProjectId);

        Assert.Equal(EscrowStatus.Completed, escrow.Status);
        Assert.Equal(Delivered, escrow.ReleasedAmount);

        var project = await db.Projects.SingleAsync(
            item => item.Id == scenario.ProjectId);

        Assert.Equal(ProjectStatus.Completed, project.Status);

        var row = await db.EscrowCases.SingleAsync(
            item => item.MilestoneId == scenario.MilestoneId);

        Assert.Equal(EscrowCaseStatus.Settled, row.Status);
        Assert.Equal(Delivered, row.PaidToFreelancer);
        Assert.Equal(0m, row.ReturnedToClient);
    }

    [Fact]
    public async Task ASplitIsRecordedExactly()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        FakeEscrowChainDeployer.SettleMilestone(
            scenario.ContractAddress, 0, Units(333.33m), endEscrow: false);

        await SyncAsync(scenario, scenario.Client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var milestone = await db.Milestones.SingleAsync(
            item => item.Id == scenario.MilestoneId);

        Assert.Equal(MileStoneStatus.Settled, milestone.Status);
        Assert.Equal(333.33m, milestone.PaidAmount);

        var row = await db.EscrowCases.SingleAsync(
            item => item.MilestoneId == scenario.MilestoneId);

        Assert.Equal(333.33m, row.PaidToFreelancer);
        Assert.Equal(666.67m, row.ReturnedToClient);

        var escrow = await db.Escrows.SingleAsync(
            item => item.ProjectId == scenario.ProjectId);

        // Only what the freelancer was paid counts as released.
        Assert.Equal(333.33m, escrow.ReleasedAmount);
    }

    [Fact]
    public async Task ARefundThatEndsTheEscrowPutsTheLaterStagesBackOnTheMarket()
    {
        var scenario = await CreateSubmittedMilestoneAsync(
            _client, milestoneCount: 3);

        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        FakeEscrowChainDeployer.SettleMilestone(
            scenario.ContractAddress, 0, Units(0m), endEscrow: true);

        await SyncAsync(scenario, scenario.Client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var escrow = await db.Escrows.SingleAsync(
            item => item.ProjectId == scenario.ProjectId);

        Assert.Equal(EscrowStatus.Terminated, escrow.Status);
        Assert.Equal(0m, escrow.ReleasedAmount);

        var milestones = await db.Milestones
            .Where(item => item.ProjectId == scenario.ProjectId)
            .OrderBy(item => item.SequenceNumber)
            .ToListAsync();

        // The one the arbiter ruled on stays as the record of what happened.
        Assert.Equal(MileStoneStatus.Settled, milestones[0].Status);
        Assert.Equal(0m, milestones[0].PaidAmount);
        Assert.Equal(scenario.Freelancer.Id, milestones[0].FreelancerId);

        // The stages that were never paid are free for someone else.
        foreach (var later in milestones.Skip(1))
        {
            Assert.Equal(MileStoneStatus.Pending, later.Status);
            Assert.Null(later.FreelancerId);
        }

        var proposal = await db.Proposals.SingleAsync(
            item => item.ProjectId == scenario.ProjectId);

        Assert.Equal(ProposalStatus.Rejected, proposal.Status);

        var project = await db.Projects.SingleAsync(
            item => item.Id == scenario.ProjectId);

        Assert.Equal(ProjectStatus.InProgress, project.Status);

        // Syncing again does not do it twice.
        await SyncAsync(scenario, scenario.Client);
        Assert.Equal(
            EscrowStatus.Terminated,
            (await db.Escrows.AsNoTracking().SingleAsync(
                item => item.ProjectId == scenario.ProjectId)).Status);
    }

    [Fact]
    public async Task AFreelancerWhoResignsFreesTheirUnpaidStages()
    {
        var scenario = await CreateSubmittedMilestoneAsync(
            _client, milestoneCount: 2);

        await OpenCaseAsync(scenario, OnChainCaseKind.PaymentRequest, 1);

        FakeEscrowChainDeployer.Resign(scenario.ContractAddress);

        await SyncAsync(scenario, scenario.Client);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(
            EscrowStatus.Terminated,
            (await db.Escrows.SingleAsync(
                item => item.ProjectId == scenario.ProjectId)).Status);

        var milestones = await db.Milestones
            .Where(item => item.ProjectId == scenario.ProjectId)
            .ToListAsync();

        Assert.All(milestones, item =>
        {
            Assert.Equal(MileStoneStatus.Pending, item.Status);
            Assert.Null(item.FreelancerId);
        });

        // The case that was open is closed, not settled.
        Assert.Equal(
            EscrowCaseStatus.Withdrawn,
            (await db.EscrowCases.SingleAsync(
                item => item.MilestoneId == scenario.MilestoneId)).Status);

        // The next freelancer does not inherit the earlier one's work.
        var delivered = await db.MilestoneSubmissions
            .CountAsync(item => item.MilestoneId == scenario.MilestoneId);

        Assert.Equal(1, delivered);

        var visible = await SendAsync(
            _client,
            HttpMethod.Get,
            $"/api/projects/{scenario.ProjectId}/milestones/" +
            $"{scenario.MilestoneId}/submissions",
            scenario.Client.Token);

        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
        Assert.Empty((await ReadAsync(visible)).EnumerateArray());
    }

    [Fact]
    public async Task TheClientCannotApproveAMilestoneTheArbiterSettled()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        FakeEscrowChainDeployer.SettleMilestone(
            scenario.ContractAddress, 0, Units(0m), endEscrow: false);

        // Before the backend has synced, the milestone still looks
        // submitted. Approving it would credit money that went back.
        var approve = await SendAsync(
            _client,
            HttpMethod.Patch,
            $"/api/projects/{scenario.ProjectId}/milestones/" +
            $"{scenario.MilestoneId}/approve",
            scenario.Client.Token);

        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(
            MileStoneStatus.Submitted,
            (await db.Milestones.SingleAsync(
                item => item.Id == scenario.MilestoneId)).Status);
    }

    [Fact]
    public async Task ALaterStageOfASettledEscrowCanStart()
    {
        var scenario = await CreateSubmittedMilestoneAsync(
            _client, milestoneCount: 2);

        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        FakeEscrowChainDeployer.SettleMilestone(
            scenario.ContractAddress, 0, Units(0m), endEscrow: false);

        await SyncAsync(scenario, scenario.Client);

        var start = await SendAsync(
            _client,
            HttpMethod.Patch,
            $"/api/projects/{scenario.ProjectId}/milestones/" +
            $"{scenario.AllMilestoneIds![1]}/start",
            scenario.Freelancer.Token);

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
    }

    // ---- the reviewer's desk --------------------------------------------

    [Fact]
    public async Task OnlyAReviewerCanUseTheDesk()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);

        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        var caseId = await CaseIdAsync(scenario);

        foreach (var person in new[] { scenario.Client, scenario.Freelancer })
        {
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await SendAsync(
                    _client, HttpMethod.Get, "/api/review/cases",
                    person.Token)).StatusCode);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await SendAsync(
                    _client, HttpMethod.Get, $"/api/review/cases/{caseId}",
                    person.Token)).StatusCode);

            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await SendAsync(
                    _client, HttpMethod.Post,
                    $"/api/review/cases/{caseId}/advice",
                    person.Token)).StatusCode);
        }

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/review/cases")).StatusCode);
    }

    [Fact]
    public async Task TheReviewerSeesWhetherTheFourteenDaysAreOver()
    {
        var early = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(early, OnChainCaseKind.RefundRequest, 3);

        var late = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(late, OnChainCaseKind.RefundRequest, 15);

        var reviewer = await NewReviewerAsync();

        var list = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/review/cases", reviewer.Token));

        var byId = list.EnumerateArray().ToDictionary(
            item => item.GetProperty("id").GetGuid());

        Assert.False(byId[await CaseIdAsync(early)]
            .GetProperty("isReady").GetBoolean());

        Assert.True(byId[await CaseIdAsync(late)]
            .GetProperty("isReady").GetBoolean());
    }

    [Fact]
    public async Task TheReviewerReadsTheEvidenceAndTheDelivery()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(scenario, OnChainCaseKind.PaymentRequest, 15);

        var caseId = await CaseIdAsync(scenario);
        var reviewer = await NewReviewerAsync();

        var detail = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, $"/api/review/cases/{caseId}",
            reviewer.Token));

        Assert.Equal(
            "Responsive landing page with a working contact form.",
            detail.GetProperty("milestoneDescription").GetString());

        Assert.Equal(0, detail.GetProperty("onChainMilestoneIndex").GetInt32());
        Assert.Equal(6, detail.GetProperty("tokenDecimals").GetInt32());
        Assert.Equal(
            "0x15d34AAf54267DB7D7c367839AAf71A00a2C6A65",
            detail.GetProperty("arbiterWalletAddress").GetString());

        var delivery = Assert.Single(
            detail.GetProperty("deliveries").EnumerateArray());

        var file = Assert.Single(
            delivery.GetProperty("attachments").EnumerateArray());

        var download = await SendAsync(
            _client,
            HttpMethod.Get,
            $"/api/review/cases/{caseId}/files/" +
            file.GetProperty("id").GetGuid(),
            reviewer.Token);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);

        // A file id that is not part of this case's delivery gives nothing.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                _client,
                HttpMethod.Get,
                $"/api/review/cases/{caseId}/files/{Guid.NewGuid()}",
                reviewer.Token)).StatusCode);
    }

    [Fact]
    public async Task AdviceIsOnlyAdviceAndIsNeverShownToTheParties()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        var caseId = await CaseIdAsync(scenario);
        var reviewer = await NewReviewerAsync();

        var advised = await SendAsync(
            _client, HttpMethod.Post, $"/api/review/cases/{caseId}/advice",
            reviewer.Token);

        Assert.Equal(HttpStatusCode.OK, advised.StatusCode);

        var advice = (await ReadAsync(advised)).GetProperty("advice");

        Assert.InRange(
            advice.GetProperty("freelancerSharePercent").GetInt32(), 0, 100);

        Assert.True(advice.GetProperty("isDemo").GetBoolean());

        var prompt = ScriptedAiModel.AllText(
            _factory.AiModel.Requests.Last());

        // The model is told what kind of case this is, by role only.
        Assert.Contains("RefundRequest", prompt);
        Assert.DoesNotContain(scenario.Client.Email, prompt);
        Assert.DoesNotContain(scenario.Freelancer.Email, prompt);

        foreach (var person in new[] { scenario.Client, scenario.Freelancer })
        {
            var cases = await ListCasesAsync(scenario, person);
            var text = cases.Single().GetRawText();

            Assert.DoesNotContain("advice", text, StringComparison.OrdinalIgnoreCase);
        }

        // Advice moved nothing: the case is still open.
        Assert.Equal(
            "Open",
            (await ListCasesAsync(scenario, scenario.Client))
                .Single().GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheReviewerNoteIsShownToBothSides()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(scenario, OnChainCaseKind.RefundRequest, 15);

        var caseId = await CaseIdAsync(scenario);
        var reviewer = await NewReviewerAsync();

        var empty = await SendAsync(
            _client, HttpMethod.Put, $"/api/review/cases/{caseId}/note",
            reviewer.Token, new { Note = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var saved = await SendAsync(
            _client, HttpMethod.Put, $"/api/review/cases/{caseId}/note",
            reviewer.Token, new { Note = "Nothing was delivered by the deadline." });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        foreach (var person in new[] { scenario.Client, scenario.Freelancer })
        {
            Assert.Equal(
                "Nothing was delivered by the deadline.",
                (await ListCasesAsync(scenario, person))
                    .Single().GetProperty("reviewNote").GetString());
        }

        // Neither side can write it.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await SendAsync(
                _client, HttpMethod.Put, $"/api/review/cases/{caseId}/note",
                scenario.Client.Token, new { Note = "I win." })).StatusCode);
    }

    [Fact]
    public async Task ThereIsNoRouteThatSettlesOrMovesAnything()
    {
        // Settling happens on the contract, signed by the arbiter's wallet.
        // The reviewer controller must offer reading, advice, a note and a
        // chain refresh, and nothing else.
        var actions = typeof(Api.Controllers.ReviewCasesController)
            .GetMethods(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Equal(
            ["Advise", "Download", "Get", "List", "SaveNote", "Sync"],
            actions);

        await Task.CompletedTask;
    }

    // ---- notifications ---------------------------------------------------

    [Fact]
    public async Task TheOtherSideAndTheReviewerAreToldAboutACase()
    {
        var scenario = await CreateSubmittedMilestoneAsync(_client);
        await OpenCaseAsync(scenario, OnChainCaseKind.PaymentRequest, 15);

        var reviewer = await NewReviewerAsync();

        var client = await NotificationsAsync(scenario.Client);
        var freelancer = await NotificationsAsync(scenario.Freelancer);
        var desk = await NotificationsAsync(reviewer);

        Assert.Contains(client, item =>
            item.GetProperty("kind").GetString() == "CaseOpen" &&
            item.GetProperty("severity").GetString() == "action");

        // The side that asked is only told it is waiting.
        Assert.Contains(freelancer, item =>
            item.GetProperty("kind").GetString() == "CaseOpen" &&
            item.GetProperty("severity").GetString() == "info");

        Assert.Contains(desk, item =>
            item.GetProperty("kind").GetString() == "ReviewReady");
    }

    // ---- helpers ----------------------------------------------------------

    private async Task OpenCaseAsync(
        SubmittedMilestone scenario,
        OnChainCaseKind kind,
        int daysAgo)
    {
        FakeEscrowChainDeployer.OpenCase(
            scenario.ContractAddress,
            kind,
            DateTimeOffset.UtcNow.AddDays(-daysAgo));

        await SyncAsync(scenario, scenario.Client);
    }

    private async Task SyncAsync(SubmittedMilestone scenario, Person who)
    {
        var path = who.Id == scenario.Client.Id
            ? $"/api/projects/{scenario.ProjectId}/escrow/sync" +
              $"?freelancerId={scenario.Freelancer.Id}"
            : $"/api/projects/{scenario.ProjectId}/escrow/sync";

        var response = await SendAsync(
            _client, HttpMethod.Post, path, who.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<JsonElement>> ListCasesAsync(
        SubmittedMilestone scenario,
        Person who)
    {
        var response = await SendAsync(
            _client,
            HttpMethod.Get,
            $"/api/projects/{scenario.ProjectId}/escrow-cases",
            who.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await ReadAsync(response)).EnumerateArray().ToList();
    }

    private async Task<Guid> CaseIdAsync(SubmittedMilestone scenario)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.EscrowCases
            .Where(item => item.MilestoneId == scenario.MilestoneId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstAsync();
    }

    private async Task<List<JsonElement>> NotificationsAsync(Person who)
    {
        var response = await SendAsync(
            _client, HttpMethod.Get, "/api/notifications", who.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await ReadAsync(response)).GetProperty("items")
            .EnumerateArray().ToList();
    }

    // A reviewer is a person who holds the Admin role. The role is given
    // directly here; the product gives it from configuration.
    private async Task<Person> NewReviewerAsync()
    {
        var person = await NewPersonAsync(_client, "Client", "Reviewer");

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await users.FindByIdAsync(person.Id.ToString());

            Assert.True((await users.AddToRoleAsync(
                user!, AppRoles.Admin)).Succeeded);
        }

        var login = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = person.Email, Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();

        return person with
        {
            Token = body.GetProperty("accessToken").GetString()!
        };
    }

    private static System.Numerics.BigInteger Units(decimal amount) =>
        TokenAmountConverter.ToBaseUnits(
            amount,
            FakeEscrowChainDeployer.TokenDecimals);
}
