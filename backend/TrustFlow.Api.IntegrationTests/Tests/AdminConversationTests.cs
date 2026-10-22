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
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminConversationTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    private static string Url(Guid project, Guid? thread = null) => $"/api/admin/projects/{project}/conversations" + (thread.HasValue ? $"/{thread}/messages" : "");

    [Fact]
    public async Task OnlyAdminsCanReadThreadListsAndMessages()
    {
        var owner = await NewPersonAsync(client, "Client", "Ordinary client");
        var talent = await NewPersonAsync(client, "Freelancer", "Ordinary freelancer");
        foreach (var path in new[] { Url(Guid.NewGuid()), Url(Guid.NewGuid(), Guid.NewGuid()) })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
            foreach (var person in new[] { owner, talent }) Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, path, person.Token)).StatusCode);
        }
    }

    [Fact]
    public async Task ListKeepsHistoricalThreadsSeparateAndDoesNotExposeBodiesOrReadState()
    {
        var s = await SeedAsync(); var ids = new List<Guid>();
        for (var page = 1; page <= 2; page++)
        {
            var body = await GetAsync(s, Url(s.Project.Id) + $"?pageSize=1&page={page}");
            Assert.Equal(2, body.GetProperty("totalItems").GetInt32());
            var row = Assert.Single(body.GetProperty("items").EnumerateArray()); ids.Add(row.GetProperty("id").GetGuid());
            Assert.Equal(s.Owner.Id, row.GetProperty("clientId").GetGuid());
            Assert.False(row.TryGetProperty("body", out _)); Assert.False(row.TryGetProperty("messages", out _));
            Assert.False(row.TryGetProperty("clientLastReadAt", out _)); Assert.False(row.TryGetProperty("email", out _));
        }
        Assert.Equal(new[] { s.Thread.Id, s.SecondThread.Id }.Order(), ids.Order());
        var first = Assert.Single((await GetAsync(s, Url(s.Project.Id) + "?pageSize=1")).GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        Assert.Equal(ids[0], first);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await db.Milestones.Where(item => item.ProjectId == s.Project.Id).ToListAsync());
        Assert.Empty(await db.Proposals.Where(item => item.ProjectId == s.Project.Id).ToListAsync());
        Assert.DoesNotContain(await db.AdminAccessLogs.Where(log => log.ProjectId == s.Project.Id).ToListAsync(), log => log.ResourceId == s.ForeignThread.Id);
    }

    [Fact]
    public async Task ReadingMessagesPreservesParticipantUnreadMarkersAndRecordsActualActorAndVisibleIds()
    {
        var s = await SeedAsync();
        var body = await GetAsync(s, Url(s.Project.Id, s.Thread.Id) + "?adminId=" + s.Owner.Id);
        var messages = body.GetProperty("messages").EnumerateArray().ToArray(); Assert.Equal(3, messages.Length);
        Assert.Equal(new[] { "Client", "Freelancer", "Client" }, messages.Select(item => item.GetProperty("senderRole").GetString()));
        Assert.Equal("Stored <script>text</script>\nSecond line", messages[0].GetProperty("body").GetString());
        Assert.Contains(messages, item => item.GetProperty("senderName").GetString() == "Reza historical");
        Assert.All(messages, item => { Assert.False(item.TryGetProperty("email", out _)); Assert.False(item.TryGetProperty("mine", out _)); });
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var thread = await db.ChatThreads.SingleAsync(item => item.Id == s.Thread.Id);
        Assert.Equal(s.Thread.ClientLastReadAt, thread.ClientLastReadAt); Assert.Equal(s.Thread.FreelancerLastReadAt, thread.FreelancerLastReadAt);
        Assert.Equal(s.Thread.LastMessageAt, thread.LastMessageAt); Assert.Equal(3, await db.ChatMessages.CountAsync(item => item.ThreadId == thread.Id));
        var logs = await db.AdminAccessLogs.Where(log => log.ProjectId == s.Project.Id).ToListAsync();
        Assert.Equal(4, logs.Count); Assert.All(logs, log => Assert.Equal(s.Admin.Id, log.AdminId));
        Assert.Contains(logs, log => log.Action == "AdminConversationMessagesViewed" && log.ResourceId == thread.Id);
        Assert.Equal(messages.Select(item => item.GetProperty("id").GetGuid()).Order(), logs.Where(log => log.Action.EndsWith("Record")).Select(log => log.ResourceId!.Value).Order());
        Assert.All(logs, log => Assert.DoesNotContain("Stored", log.Action));
        Assert.Equal(ProjectStatus.Completed, (await db.Projects.SingleAsync(item => item.Id == s.Project.Id)).Status);
    }

    [Fact]
    public async Task OlderMessageCursorPagesPastTwoHundredWithTimestampTiesAndExcludesNewArrivals()
    {
        var s = await SeedAsync(420); var all = new List<Guid>(); Guid? before = null; Guid? through = null;
        var timestamp = s.Thread.LastMessageAt;
        for (var page = 0; page < 20; page++)
        {
            var body = await GetAsync(s, Url(s.Project.Id, s.Thread.Id) + "?pageSize=50" + (before.HasValue ? "&beforeMessageId=" + before : "") + (through.HasValue ? "&throughMessageId=" + through : ""));
            through ??= body.GetProperty("throughMessageId").GetGuid();
            var messages = body.GetProperty("messages").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
            Assert.Equal(messages.Order(), messages); all.AddRange(messages);
            if (page == 0)
            {
                using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.ChatMessages.Add(new ChatMessage { ThreadId = s.Thread.Id, SenderId = s.Owner.Id, Body = "New arrival after snapshot", SentAt = timestamp.AddMinutes(1) });
                await db.SaveChangesAsync();
            }
            if (!body.GetProperty("hasOlderMessages").GetBoolean()) break;
            before = body.GetProperty("nextBeforeMessageId").GetGuid();
        }
        Assert.Equal(420, all.Count); Assert.Equal(420, all.Distinct().Count());
        var newest = await GetAsync(s, Url(s.Project.Id, s.Thread.Id));
        Assert.Contains(newest.GetProperty("messages").EnumerateArray(), item => item.GetProperty("body").GetString() == "New arrival after snapshot");
        var pinned = await GetAsync(s, Url(s.Project.Id, s.Thread.Id) + "?throughMessageId=" + through);
        Assert.DoesNotContain(pinned.GetProperty("messages").EnumerateArray(), item => item.GetProperty("body").GetString() == "New arrival after snapshot");
    }

    [Fact]
    public async Task InvalidAndCrossThreadCursorsCannotLeakMessagesOrWriteSuccessfulReadAudits()
    {
        var s = await SeedAsync(); Guid foreignMessage;
        using (var scope = factory.Services.CreateScope())
            foreignMessage = await scope.ServiceProvider.GetRequiredService<AppDbContext>().ChatMessages.Where(item => item.ThreadId == s.ForeignThread.Id).Select(item => item.Id).FirstAsync();
        foreach (var path in new[] { Url(Guid.NewGuid()), Url(s.Project.Id, s.ForeignThread.Id), Url(s.ForeignThread.ProjectId, s.Thread.Id),
            Url(s.Project.Id, s.Thread.Id) + "?beforeMessageId=" + foreignMessage, Url(s.Project.Id, s.Thread.Id) + "?throughMessageId=" + foreignMessage,
            Url(s.Project.Id) + "?page=0", Url(s.Project.Id, s.Thread.Id) + "?pageSize=101", Url(s.Project.Id, s.Thread.Id) + "?pageSize=0" })
            Assert.Contains((await SendAsync(client, HttpMethod.Get, path, s.Admin.Token)).StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.BadRequest });
        using var checkScope = factory.Services.CreateScope(); var db = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.ProjectId == s.Project.Id));
        var post = await SendAsync(client, HttpMethod.Post, Url(s.Project.Id, s.Thread.Id), s.Admin.Token, new { body = "Admin cannot reply" });
        Assert.Contains(post.StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        Assert.Equal(3, await db.ChatMessages.CountAsync(item => item.ThreadId == s.Thread.Id));
    }

    [Fact]
    public async Task ExistingEmptyProjectsAndThreadsDoNotCreateMessagesOrReadMarkers()
    {
        var s = await SeedAsync(0);
        var empty = await GetAsync(s, Url(s.Project.Id, s.Thread.Id));
        Assert.Empty(empty.GetProperty("messages").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("throughMessageId").ValueKind); Assert.False(empty.GetProperty("hasOlderMessages").GetBoolean());
        Guid emptyProject;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var project = new Project { ClientId = s.Owner.Id, Title = "No conversations", Budget = 1, Deadline = DateTimeOffset.UtcNow };
            db.Projects.Add(project); await db.SaveChangesAsync(); emptyProject = project.Id;
        }
        var list = await GetAsync(s, Url(emptyProject)); Assert.Empty(list.GetProperty("items").EnumerateArray());
        using var checkScope = factory.Services.CreateScope(); var check = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await check.ChatThreads.AnyAsync(item => item.ProjectId == emptyProject));
        Assert.Equal(DateTimeOffset.UnixEpoch, (await check.ChatThreads.SingleAsync(item => item.Id == s.Thread.Id)).ClientLastReadAt);
    }

    private async Task<JsonElement> GetAsync(Scenario s, string path)
    {
        var response = await SendAsync(client, HttpMethod.Get, path, s.Admin.Token); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private); Assert.True(response.Headers.CacheControl?.NoStore); return await ReadAsync(response);
    }
    private async Task<Scenario> SeedAsync(int messageCount = 3)
    {
        var admin = await NewPersonAsync(client, "Client", "Conversation administrator");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        }
        admin = admin with { Token = (await ReadAsync(await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password))).GetProperty("accessToken").GetString()! };
        var owner = await NewPersonAsync(client, "Client", "Sara history");
        var talent = await NewPersonAsync(client, "Freelancer", "Reza historical"); var other = await NewPersonAsync(client, "Freelancer", "Other freelancer");
        var timestamp = new DateTimeOffset(2026,10,19,12,0,0,TimeSpan.Zero);
        var project = new Project { ClientId = owner.Id, Title = "Completed with historical conversations", Budget = 100, Deadline = timestamp, Status = ProjectStatus.Completed };
        var foreignProject = new Project { ClientId = owner.Id, Title = "Unrelated project", Budget = 1, Deadline = timestamp };
        var thread = new ChatThread { ProjectId = project.Id, FreelancerId = talent.Id, LastMessageAt = timestamp };
        var second = new ChatThread { ProjectId = project.Id, FreelancerId = other.Id, LastMessageAt = timestamp };
        var foreign = new ChatThread { ProjectId = foreignProject.Id, FreelancerId = talent.Id, LastMessageAt = timestamp };
        using var seed = factory.Services.CreateScope(); var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.AddRange(project, foreignProject); db.ChatThreads.AddRange(thread, second, foreign);
        var prefix = Guid.NewGuid().ToString("N")[..8];
        db.ChatMessages.AddRange(Enumerable.Range(0,messageCount).Select(index => new ChatMessage { ThreadId = thread.Id,
            SenderId = index % 2 == 0 ? owner.Id : talent.Id, SentAt = timestamp, Body = index == 0 ? "Stored <script>text</script>\nSecond line" : "Work message " + index,
            Id = Guid.Parse($"{prefix}-0000-0000-0000-{index+1:D12}") }));
        db.ChatMessages.Add(new ChatMessage { ThreadId = second.Id, SenderId = other.Id, Body = "Separate freelancer conversation", SentAt = timestamp });
        db.ChatMessages.Add(new ChatMessage { ThreadId = foreign.Id, SenderId = owner.Id, Body = "Unrelated message never returned", SentAt = timestamp });
        await db.SaveChangesAsync();
        return new Scenario(admin, owner, project, thread, second, foreign);
    }
    private sealed record Scenario(Person Admin, Person Owner, Project Project, ChatThread Thread, ChatThread SecondThread, ChatThread ForeignThread);
}
