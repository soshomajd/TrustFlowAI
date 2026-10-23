using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Ai;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminTimelineTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    private static string Url(Guid project) => $"/api/admin/projects/{project}/timeline";

    [Fact]
    public async Task OrdinaryAccountsCannotReadTimelineOrAuditMetadata()
    {
        var owner = await NewPersonAsync(client, "Client", "Timeline client");
        var talent = await NewPersonAsync(client, "Freelancer", "Timeline talent");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url(Guid.NewGuid()))).StatusCode);
        foreach (var person in new[] { owner, talent })
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, Url(Guid.NewGuid()), person.Token)).StatusCode);
    }

    [Fact]
    public async Task TimelineIncludesSavedLifecycleAndMetadataButNoPrivateBodiesOrInferredActors()
    {
        var s = await SeedAsync(); var body = await GetAsync(s, "?pageSize=100&category=all");
        var rows = body.GetProperty("items").EnumerateArray().ToArray();
        foreach (var code in new[] { 1, 10, 20, 30, 31, 32, 40, 41, 43, 50, 51, 60, 61, 62, 63, 70, 71, 72 })
            Assert.Contains(rows, item => item.GetProperty("code").GetInt32() == code);
        Assert.DoesNotContain(rows, item => item.GetProperty("category").GetString() == "admin");
        Assert.All(rows, item => Assert.Equal(JsonValueKind.String, item.GetProperty("occurredAt").ValueKind));
        var created = Assert.Single(rows, item => item.GetProperty("code").GetInt32() == 1);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("actorId").ValueKind);
        var message = Assert.Single(rows, item => item.GetProperty("code").GetInt32() == 51);
        Assert.Equal(s.Talent.Id, message.GetProperty("actorId").GetGuid());
        Assert.Equal("Historical author", message.GetProperty("actorName").GetString());
        var funding = Assert.Single(rows, item => item.GetProperty("code").GetInt32() == 32);
        Assert.Equal("saved-funding-reference", funding.GetProperty("transactionHash").GetString());
        Assert.Equal(s.At, funding.GetProperty("occurredAt").GetDateTimeOffset());
        var raw = body.GetRawText();
        foreach (var secret in new[] { "PRIVATE MESSAGE", "PRIVATE STATEMENT", "PRIVATE ADVICE", "PRIVATE FEEDBACK", "PRIVATE DELIVERY", "PRIVATE RUN", "@trustflow.test" })
            Assert.DoesNotContain(secret, raw);
    }

    [Fact]
    public async Task MissingTimesNeverBorrowCreatedUpdatedOrDeadlineDates()
    {
        var s = await SeedAsync(); var body = await GetAsync(s, "?timing=missing&pageSize=100");
        var rows = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(rows, item => item.GetProperty("code").GetInt32() == 11);
        Assert.Contains(rows, item => item.GetProperty("code").GetInt32() == 12);
        Assert.Contains(rows, item => item.GetProperty("code").GetInt32() == 21);
        Assert.Contains(rows, item => item.GetProperty("code").GetInt32() == 42);
        Assert.All(rows, item => Assert.Equal(JsonValueKind.Null, item.GetProperty("occurredAt").ValueKind));
        Assert.All(rows.Where(item => item.GetProperty("code").GetInt32() == 11), item => Assert.Equal(JsonValueKind.Null, item.GetProperty("transactionHash").ValueKind));
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Get, Url(s.Project.Id) + "?timing=missing&from=2026-01-01", s.Admin.Token)).StatusCode);
    }

    [Fact]
    public async Task StableCursorWalksTimestampTiesBeyondTwoHundredWithoutNewArrivalsOrOwnAuditLoop()
    {
        var s = await SeedAsync(220); var ids = new List<Guid>(); string? cursor = null;
        for (var index = 0; index < 30; index++)
        {
            var result = await GetAsync(s, "?category=conversations&pageSize=17" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            ids.AddRange(result.GetProperty("items").EnumerateArray().Where(item => item.GetProperty("code").GetInt32() == 51).Select(item => item.GetProperty("resourceId").GetGuid()));
            if (index == 0)
            {
                using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ChatMessages.Add(new ChatMessage { ThreadId = s.Thread.Id, SenderId = s.Owner.Id, Body = "Later arrival", SentAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            cursor = result.GetProperty("nextCursor").GetString(); if (cursor is null) break;
        }
        Assert.Equal(220, ids.Count); Assert.Equal(220, ids.Distinct().Count());
        Assert.Equal(ids.Order(), ids);
        var auditIds = new List<Guid>(); cursor = null;
        for (var index = 0; index < 80; index++)
        {
            var body = await GetAsync(s, "?category=admin&pageSize=13" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
            auditIds.AddRange(body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("resourceId").GetGuid()));
            cursor = body.GetProperty("nextCursor").GetString(); if (cursor is null) break;
        }
        Assert.Null(cursor); Assert.Equal(auditIds.Count, auditIds.Distinct().Count());
    }

    [Fact]
    public async Task FiltersAndProtectedCursorsStayBoundToProjectAndView()
    {
        var s = await SeedAsync(5);
        var first = await GetAsync(s, "?category=conversations&pageSize=1"); var cursor = first.GetProperty("nextCursor").GetString()!;
        foreach (var suffix in new[] { "?cursor=bad", "?category=admin&cursor=" + Uri.EscapeDataString(cursor), "?timing=missing&cursor=" + Uri.EscapeDataString(cursor),
            "?pageSize=0", "?pageSize=101", "?category=nope", "?timing=nope", "?from=2026-10-20&to=2026-10-18" })
            Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Get, Url(s.Project.Id) + suffix, s.Admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Get, Url(s.ForeignProject.Id) + "?category=conversations&cursor=" + Uri.EscapeDataString(cursor), s.Admin.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(client, HttpMethod.Get, Url(Guid.NewGuid()), s.Admin.Token)).StatusCode);
        var filtered = (await GetAsync(s, "?actorId=" + s.Talent.Id + "&category=conversations&from=2026-10-18T00:00:00Z&to=2026-10-18T23:59:59Z")).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(5, filtered.Length); Assert.All(filtered, item => Assert.Equal(s.Talent.Id, item.GetProperty("actorId").GetGuid()));
        Assert.Empty((await GetAsync(s, "?from=2000-01-01&to=2000-01-02")).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task CorruptCrossProjectDisputesAreExcludedAndReadsOnlyWriteIndependentAudits()
    {
        var s = await SeedAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var foreign = new MileStone { ProjectId = s.ForeignProject.Id, Title = "Foreign secret", Amount = 1, SequenceNumber = 1 };
            db.Milestones.Add(foreign);
            db.EscrowCases.Add(new EscrowCase { EscrowId = s.Escrow.Id, MilestoneId = foreign.Id, OpenedAt = s.At, Status = EscrowCaseStatus.Settled });
            db.Disputes.Add(new Dispute { ProjectId = s.Project.Id, MilestoneId = foreign.Id, OpenedByUserId = s.Owner.Id, RespondentUserId = s.Talent.Id, CreatedAt = s.At });
            await db.SaveChangesAsync();
        }
        var body = await GetAsync(s, "?category=disputes&pageSize=100&adminId=" + s.Owner.Id);
        Assert.Equal(7, body.GetProperty("items").GetArrayLength());
        Assert.DoesNotContain("Foreign secret", body.GetRawText());
        using var check = factory.Services.CreateScope(); var dbCheck = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var thread = await dbCheck.ChatThreads.SingleAsync(item => item.Id == s.Thread.Id);
        Assert.Equal(DateTimeOffset.UnixEpoch, thread.ClientLastReadAt); Assert.Equal(DateTimeOffset.UnixEpoch, thread.FreelancerLastReadAt);
        Assert.Equal(EscrowStatus.Funded, (await dbCheck.Escrows.SingleAsync(item => item.Id == s.Escrow.Id)).Status);
        Assert.Equal(0, (await dbCheck.Escrows.SingleAsync(item => item.Id == s.Escrow.Id)).ReleasedAmount);
        var logs = await dbCheck.AdminAccessLogs.Where(item => item.ProjectId == s.Project.Id && item.Action.StartsWith("AdminTimelineViewed")).ToListAsync();
        Assert.Equal(8, logs.Count); Assert.All(logs, item => Assert.Equal(s.Admin.Id, item.AdminId));
        Assert.Equal(body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("resourceId").GetGuid()).Order(), logs.Where(item => item.ResourceId.HasValue).Select(item => item.ResourceId!.Value).Order());
    }

    private async Task<JsonElement> GetAsync(Scenario s, string query)
    {
        var response = await SendAsync(client, HttpMethod.Get, Url(s.Project.Id) + query, s.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl?.Private); Assert.True(response.Headers.CacheControl?.NoStore);
        return await ReadAsync(response);
    }
    private async Task<Scenario> SeedAsync(int messages = 1)
    {
        var admin = await NewPersonAsync(client, "Client", "Timeline administrator");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        }
        admin = admin with { Token = (await ReadAsync(await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password))).GetProperty("accessToken").GetString()! };
        var owner = await NewPersonAsync(client, "Client", "Timeline owner"); var talent = await NewPersonAsync(client, "Freelancer", "Historical author");
        var at = new DateTimeOffset(2026,10,18,12,0,0,TimeSpan.Zero);
        var project = new Project { ClientId = owner.Id, Title = "Timeline sample", Budget = 100, CreatedAt = at, Deadline = at.AddMonths(1) };
        var foreignProject = new Project { ClientId = owner.Id, Title = "Other project", Budget = 100, CreatedAt = at, Deadline = at.AddMonths(1) };
        var milestone = new MileStone { ProjectId = project.Id, Title = "Saved stage", Amount = 100, SequenceNumber = 1, CreatedAt = at, Status = MileStoneStatus.Approved, PaidAmount = 100 };
        var escrow = new Escrow { ProjectId = project.Id, FreelancerId = talent.Id, ChainId = 31337, TokenAddress = "test-token", TotalAmount = 100,
            Status = EscrowStatus.Funded, CreatedAt = at, UpdatedAt = at.AddDays(2), DeployedAt = at, FundedAt = at, ContractAddress = "0x" + Guid.NewGuid().ToString("N") + "00000000", FundingTransactionHash = "saved-funding-reference" };
        var thread = new ChatThread { ProjectId = project.Id, FreelancerId = talent.Id, CreatedAt = at, LastMessageAt = at };
        var delivery = new MilestoneSubmission { MilestoneId = milestone.Id, FreelancerId = talent.Id, Version = 1, SubmittedAt = at, ReviewedAt = at,
            Note = "PRIVATE DELIVERY", ClientFeedback = "PRIVATE FEEDBACK" };
        using var seed = factory.Services.CreateScope(); var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.AddRange(project, foreignProject); db.Milestones.Add(milestone); db.Escrows.Add(escrow); db.ChatThreads.Add(thread);
        db.Proposals.Add(new Proposal { ProjectId = project.Id, FreelancerId = talent.Id, BidAmount = 100, EstimatedDays = 1, Status = ProposalStatus.Accepted, CreatedAt = at });
        db.MilestoneSubmissions.Add(delivery); db.MilestoneSubmissionAttachments.Add(new MilestoneSubmissionAttachment { SubmissionId = delivery.Id, OriginalFileName = "sample.txt", Content = [1], FileSize = 1 });
        db.MilestoneAiReviews.Add(new MilestoneAiReview { SubmissionId = delivery.Id, CreatedAt = at, ReviewJson = "PRIVATE RUN" });
        db.EscrowCases.Add(new EscrowCase { EscrowId = escrow.Id, MilestoneId = milestone.Id, CreatedAt = at, OpenedAt = at,
            ClosedAt = at, Status = EscrowCaseStatus.Settled, ReviewNote = "PRIVATE ADVICE", ReviewNoteAt = at, AdviceJson = "PRIVATE ADVICE", AdviceAt = at });
        db.Disputes.Add(new Dispute { ProjectId = project.Id, MilestoneId = milestone.Id, CreatedAt = at, OpenedByUserId = owner.Id, RespondentUserId = talent.Id,
            OpenerStatement = "PRIVATE STATEMENT", RespondentStatement = "PRIVATE STATEMENT", RespondentStatementAt = at, RulingJson = "PRIVATE ADVICE", RuledAt = at });
        db.AdminAccessLogs.Add(new AdminAccessLog { ProjectId = project.Id, AdminId = admin.Id, Action = "AdminDeliveryFileDownloaded", CreatedAt = at });
        var prefix = Guid.NewGuid().ToString("N")[..8];
        db.ChatMessages.AddRange(Enumerable.Range(1,messages).Select(index => new ChatMessage { ThreadId = thread.Id, SenderId = talent.Id,
            SentAt = at, Body = "PRIVATE MESSAGE", Id = Guid.Parse($"{prefix}-0000-0000-0000-{index:D12}") }));
        await db.SaveChangesAsync(); return new Scenario(admin, owner, talent, project, foreignProject, milestone, escrow, thread, at);
    }
    private sealed record Scenario(Person Admin, Person Owner, Person Talent, Project Project, Project ForeignProject, MileStone Milestone, Escrow Escrow, ChatThread Thread, DateTimeOffset At);
}
