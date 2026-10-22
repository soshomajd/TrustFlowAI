using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Admin;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Ai;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminDisputeTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    private static string Url(Guid project, string? kind = null, Guid? id = null) => $"/api/admin/projects/{project}/disputes" + (id.HasValue ? $"/{kind}/{id}" : kind is null ? "" : $"?kind={kind}");

    [Fact]
    public async Task NonAdminsCannotReadDisputesOrDetails()
    {
        foreach (var person in new[] { await NewPersonAsync(client, "Client", "Client"), await NewPersonAsync(client, "Freelancer", "Freelancer") })
            foreach (var path in new[] { Url(Guid.NewGuid()), Url(Guid.NewGuid(), "written"), Url(Guid.NewGuid(), "escrow", Guid.NewGuid()), Url(Guid.NewGuid(), "written", Guid.NewGuid()) })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, path, person.Token)).StatusCode);
            }
    }

    [Fact]
    public async Task BothRecordTypesPageStablyAndListsExcludeStatementsAndAdvice()
    {
        var s = await SeedAsync(); var ids = new List<Guid>();
        for (var page = 1; page <= 2; page++)
        {
            var data = await GetAsync(s, Url(s.Project.Id) + $"?pageSize=1&page={page}");
            Assert.Equal(2, data.GetProperty("totalItems").GetInt32());
            var row = Assert.Single(data.GetProperty("items").EnumerateArray()); ids.Add(row.GetProperty("id").GetGuid());
            Assert.Equal("escrow", row.GetProperty("recordType").GetString());
            foreach (var field in new[] { "openerStatement", "opinion", "reviewNote", "email", "rulingJson" }) Assert.False(row.TryGetProperty(field, out _));
        }
        Assert.Equal(new[] { s.Open.Id, s.Settled.Id }.Order(), ids.Order());
        var written = Assert.Single((await GetAsync(s, Url(s.Project.Id, "written"))).GetProperty("items").EnumerateArray());
        Assert.Equal(s.Talent.Id, written.GetProperty("freelancerId").GetGuid());
        Assert.Equal("Original freelancer", written.GetProperty("freelancerName").GetString());
        Assert.Equal("9007199254740991.01", written.GetProperty("milestoneAmountExact").GetString());
        Assert.Equal(JsonValueKind.Null, written.GetProperty("reviewableAt").ValueKind);
    }

    [Fact]
    public async Task EscrowDetailsKeepTimingsIdentityNoteAndHistoricalAmounts_WithoutAttachingUnrelatedStatements()
    {
        var s = await SeedAsync();
        var open = await GetAsync(s, Url(s.Project.Id, "escrow", s.Open.Id)); var record = open.GetProperty("record");
        Assert.Equal(s.Open.OpenedAt.AddDays(14), record.GetProperty("reviewableAt").GetDateTimeOffset());
        Assert.Equal(s.Open.OpenedAt.AddDays(90), record.GetProperty("fallbackAt").GetDateTimeOffset());
        Assert.Equal("PaymentRequest", record.GetProperty("caseKind").GetString());
        Assert.Equal("snapshot-arbiter", open.GetProperty("arbiterWalletAddress").GetString());
        Assert.Equal("Review <note>", open.GetProperty("reviewNote").GetString());
        Assert.Equal(JsonValueKind.Null, open.GetProperty("openerStatement").ValueKind);
        Assert.Equal(JsonValueKind.Null, open.GetProperty("outcome").ValueKind);
        var settled = await GetAsync(s, Url(s.Project.Id, "escrow", s.Settled.Id)); var outcome = settled.GetProperty("outcome");
        foreach (var pair in new[] { ("grossAmountExact", 20m), ("platformFeePaidExact", 2m), ("freelancerNetPaidExact", 18m), ("returnedToClientExact", 30m) })
            Assert.Equal(pair.Item2, decimal.Parse(outcome.GetProperty(pair.Item1).GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(outcome.GetProperty("escrowEnded").GetBoolean());
    }

    [Fact]
    public async Task WrittenDetailsKeepOriginalPartiesAndSafeSavedOpinionWithoutGeneratingOrSettlingAnything()
    {
        var s = await SeedAsync(); var body = await GetAsync(s, Url(s.Project.Id, "written", s.Written.Id) + "?adminId=" + s.Owner.Id);
        Assert.Equal(s.Written.OpenerStatement, body.GetProperty("openerStatement").GetString());
        Assert.Equal("Original response", body.GetProperty("respondentStatement").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("outcome").ValueKind);
        var opinion = body.GetProperty("opinion"); Assert.True(opinion.GetProperty("readable").GetBoolean());
        Assert.True(opinion.GetProperty("isDemo").GetBoolean()); Assert.Equal("Saved <summary>", opinion.GetProperty("summary").GetString());
        Assert.False(opinion.TryGetProperty("systemPrompt", out _)); Assert.False(body.TryGetProperty("rulingJson", out _));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Disputes.SingleAsync(item => item.Id == s.Written.Id);
        Assert.Equal(s.Written.RulingJson, saved.RulingJson); Assert.Equal(s.Written.RulingAttempts, saved.RulingAttempts);
        Assert.Equal(1, await db.AiRuns.CountAsync(item => item.UserId == s.Owner.Id));
        Assert.Equal(EscrowCaseStatus.Open, (await db.EscrowCases.SingleAsync(item => item.Id == s.Open.Id)).Status);
        var log = await db.AdminAccessLogs.SingleAsync(item => item.ProjectId == s.Project.Id && item.Action == "AdminDisputeViewed");
        Assert.Equal(s.Admin.Id, log.AdminId); Assert.Equal(s.Written.Id, log.ResourceId);
    }

    [Fact]
    public async Task MalformedAdviceAndUnmatchedHistoricalFeeRemainUnknownWithoutFailingTheCase()
    {
        var s = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c = await db.EscrowCases.SingleAsync(item => item.Id == s.Settled.Id); c.AdviceJson = "{broken"; c.AdviceAt = DateTimeOffset.UtcNow;
            var stage = await db.Milestones.SingleAsync(item => item.Id == c.MilestoneId); stage.FreelancerId = s.Talent.Id;
            await db.SaveChangesAsync();
        }
        var body = await GetAsync(s, Url(s.Project.Id, "escrow", s.Settled.Id));
        Assert.False(body.GetProperty("opinion").GetProperty("readable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("opinion").GetProperty("isDemo").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("outcome").GetProperty("platformFeePaidExact").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("outcome").GetProperty("freelancerNetPaidExact").ValueKind);
    }

    [Fact]
    public async Task MissingCrossProjectAndInvalidRequestsDoNotAuditSuccessfulAccessOrExposeRecords()
    {
        var s = await SeedAsync();
        foreach (var path in new[] { Url(Guid.NewGuid()), Url(Guid.NewGuid(), "escrow", s.Open.Id), Url(Guid.NewGuid(), "written", s.Written.Id),
            Url(s.Project.Id, "wrong", s.Open.Id), Url(s.Project.Id, "written", s.Open.Id), Url(s.Project.Id) + "?kind=wrong", Url(s.Project.Id) + "?page=0", Url(s.Project.Id) + "?pageSize=101" })
            Assert.Contains((await SendAsync(client, HttpMethod.Get, path, s.Admin.Token)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AdminAccessLogs.AnyAsync(item => item.ProjectId == s.Project.Id));
        var post = await SendAsync(client, HttpMethod.Post, Url(s.Project.Id, "escrow", s.Open.Id), s.Admin.Token, new { paidToFreelancer = 100 });
        Assert.Contains(post.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }

    [Fact]
    public async Task CorruptCrossProjectRelationshipsAreExcludedAndEmptyProjectsRemainEmpty()
    {
        var s = await SeedAsync(); Guid projectId; Guid caseId; Guid writtenId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var other = new Project { ClientId = s.Owner.Id, Title = "Unrelated stage", Budget = 1, Deadline = DateTimeOffset.UtcNow };
            var stage = new MileStone { ProjectId = other.Id, Title = "Foreign stage", Amount = 1, SequenceNumber = 1, Deadline = DateTimeOffset.UtcNow };
            var badCase = new EscrowCase { EscrowId = s.Open.EscrowId, MilestoneId = stage.Id, Status = EscrowCaseStatus.Withdrawn, OpenedAt = DateTimeOffset.UtcNow };
            var badWritten = new Dispute { ProjectId = s.Project.Id, MilestoneId = stage.Id, OpenedByUserId = s.Owner.Id, RespondentUserId = s.Talent.Id,
                OpenedByRole = "Client", Status = DisputeStatus.Ruled, OpenerStatement = "Foreign statement" };
            db.Projects.Add(other); db.Milestones.Add(stage); db.EscrowCases.Add(badCase); db.Disputes.Add(badWritten); await db.SaveChangesAsync();
            projectId = other.Id; caseId = badCase.Id; writtenId = badWritten.Id;
        }
        Assert.Equal(2, (await GetAsync(s, Url(s.Project.Id))).GetProperty("totalItems").GetInt32());
        Assert.Equal(1, (await GetAsync(s, Url(s.Project.Id,"written"))).GetProperty("totalItems").GetInt32());
        foreach (var path in new[] { Url(s.Project.Id,"escrow",caseId), Url(s.Project.Id,"written",writtenId), Url(projectId,"escrow",caseId), Url(projectId,"written",writtenId) })
            Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client,HttpMethod.Get,path,s.Admin.Token)).StatusCode);
        Assert.Empty((await GetAsync(s, Url(projectId))).GetProperty("items").EnumerateArray());
        Assert.Empty((await GetAsync(s, Url(projectId,"written"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task DemoLabelUsesRecordedProviderAndPartialMissingOpinionTextIsNotDiscarded()
    {
        var s = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.AiRuns.SingleAsync(item => item.Id == s.Written.AiRunId)).Provider = "Anthropic";
            (await db.Disputes.SingleAsync(item => item.Id == s.Written.Id)).RulingJson = null;
            await db.SaveChangesAsync();
        }
        var body = await GetAsync(s, Url(s.Project.Id,"written",s.Written.Id));
        var opinion = body.GetProperty("opinion"); Assert.False(opinion.GetProperty("isDemo").GetBoolean());
        Assert.False(opinion.GetProperty("readable").GetBoolean()); Assert.Equal(60,opinion.GetProperty("freelancerSharePercent").GetInt32());
        Assert.Equal("Split",opinion.GetProperty("verdict").GetString());
    }

    [Fact]
    public void ExtremeRecordedTimesDoNotOverflowDeadlineDisplay()
    {
        var row = new AdminDisputeSummary(Guid.NewGuid(), "escrow", Guid.NewGuid(), "Stage", .01m, "Open", "Client", null, "Former", null, "Former", DateTimeOffset.MaxValue, null);
        Assert.Null(row.ReviewableAt); Assert.Null(row.FallbackAt);
    }

    private async Task<JsonElement> GetAsync(Scenario s, string path)
    {
        var response = await SendAsync(client, HttpMethod.Get, path, s.Admin.Token); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore); Assert.True(response.Headers.CacheControl?.Private); return await ReadAsync(response);
    }
    private async Task<Scenario> SeedAsync()
    {
        var admin = await NewPersonAsync(client, "Client", "Dispute administrator");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        }
        admin = admin with { Token = (await ReadAsync(await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password))).GetProperty("accessToken").GetString()! };
        var owner = await NewPersonAsync(client, "Client", "Owner"); var talent = await NewPersonAsync(client, "Freelancer", "Original freelancer");
        var other = await NewPersonAsync(client, "Freelancer", "Later freelancer"); var now = new DateTimeOffset(2026,10,20,12,0,0,TimeSpan.Zero);
        var project = new Project { ClientId = owner.Id, Title = "Dispute histories", Budget = 9007199254740991.01m, Deadline = now };
        var stages = new[] {
            new MileStone { ProjectId = project.Id, FreelancerId = talent.Id, Title = "Open case stage", Amount = 100, SequenceNumber = 1, Deadline = now },
            new MileStone { ProjectId = project.Id, FreelancerId = other.Id, Title = "Historic stage", Amount = 9007199254740991.01m, SequenceNumber = 2, Deadline = now, PaidAmount = 20, PlatformFeePaid = 2, Status = MileStoneStatus.Settled }
        };
        var escrow = new Escrow { ProjectId = project.Id, FreelancerId = talent.Id, ChainId = 31337, TotalAmount = 100, TokenAddress = "token", ArbiterWalletAddress = "snapshot-arbiter" };
        var second = new Escrow { ProjectId = project.Id, FreelancerId = other.Id, ChainId = 1, TotalAmount = 100, TokenAddress = "other-token" };
        var open = new EscrowCase { EscrowId = escrow.Id, MilestoneId = stages[0].Id, Status = EscrowCaseStatus.Open, OpenedAt = now.AddDays(-10), ReviewNote = "Review <note>" };
        var settled = new EscrowCase { EscrowId = second.Id, MilestoneId = stages[1].Id, Status = EscrowCaseStatus.Settled, OpenedAt = now.AddDays(-30), ClosedAt = now,
            PaidToFreelancer = 20, PlatformFeePaid = 0, ReturnedToClient = 30, EscrowEnded = true };
        var run = new AiRun { UserId = owner.Id, Purpose = AiPurpose.DisputeRuling, Provider = "Mock", Model = "demo", Status = AiRunStatus.Succeeded };
        var written = new Dispute { ProjectId = project.Id, MilestoneId = stages[1].Id, OpenedByRole = "Freelancer", OpenedByUserId = talent.Id, RespondentUserId = owner.Id,
            Status = DisputeStatus.Ruled, OpenerStatement = "Original <statement>", RespondentStatement = "Original response", CreatedAt = now.AddDays(-2), RuledAt = now,
            FreelancerSharePercent = 60, Verdict = DisputeVerdict.Split, AiRunId = run.Id, RulingJson = "{\"summary\":\"Saved <summary>\",\"reasoning\":\"Saved reasoning\",\"systemPrompt\":\"Never expose this field\"}" };
        using var seed = factory.Services.CreateScope(); var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.Add(project); db.Milestones.AddRange(stages); db.Escrows.AddRange(escrow,second); db.EscrowCases.AddRange(open,settled); db.AiRuns.Add(run); db.Disputes.Add(written);
        await db.SaveChangesAsync(); return new Scenario(admin,owner,talent,project,open,settled,written);
    }
    private sealed record Scenario(Person Admin, Person Owner, Person Talent, Project Project, EscrowCase Open, EscrowCase Settled, Dispute Written);
}
