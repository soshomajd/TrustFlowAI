using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models;
using TrustFlow.Api.Services.Chat;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

// What the filter lets through and what it stops, then the rules around a
// conversation: who can open one, who sees it, what happens to it when the
// project ends, and how unread messages reach the notification bell.
public sealed class ChatTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    private readonly HttpClient _client;

    public ChatTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;

        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }

    // ---- the contact filter ---------------------------------------------

    [Theory]
    [InlineData("call me on 09123456789")]
    [InlineData("my number is +98 912 345 6789")]
    [InlineData("it is +1 (555) 123-4567")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹")]
    [InlineData("0 9 1 2 3 4 5 6 7 8 9")]
    [InlineData("zero nine one two three four five six seven eight")]
    [InlineData("mail me at john@example.com")]
    [InlineData("john (at) example (dot) com")]
    [InlineData("john at gmail dot com")]
    [InlineData("ｊｏｈｎ＠ｅｘａｍｐｌｅ．ｃｏｍ")]
    [InlineData("see https://example.com/me")]
    [InlineData("www.mysite.io")]
    [InlineData("check mysite.dev/portfolio")]
    [InlineData("t.me/somebody")]
    [InlineData("add me on telegram")]
    [InlineData("my whatsapp is on my profile")]
    [InlineData("telegram id: somebody")]
    [InlineData("dm me on instagram")]
    [InlineData("find me at @john_doe")]
    [InlineData("w​w​w.​example.com")]
    public void ContactDetailsAreFound(string text)
    {
        Assert.NotEmpty(ContactInfoDetector.Find(text));
    }

    [Theory]
    [InlineData("Milestone 2 is due 2026-10-13, budget 1500 USD.")]
    [InlineData("I can start on 12/10/2026.")]
    [InlineData("We agreed 1,500,000 rials for the whole job.")]
    [InlineData("1500 USD for stage 1 and 2500 for stage 2.")]
    [InlineData("Post it on Facebook and Instagram every Friday.")]
    [InlineData("Use @media queries and the @Injectable decorator.")]
    [InlineData("Message me when it is done.")]
    [InlineData("The contract is 0x70997970C51812dc3A010C7d01b50e0d17dc79C8 on the test network.")]
    [InlineData("Tell me about the Telegram bot you need.")]
    [InlineData("I will finish in 3 days.Okay?")]
    [InlineData("It is done.in a day I will send it.")]
    [InlineData("Version 2.0 of the design is ready for review.")]
    [InlineData("سلام، پروژه آماده است")]
    public void OrdinaryWorkTalkIsNotStopped(string text)
    {
        Assert.Empty(ContactInfoDetector.Find(text));
    }

    [Fact]
    public async Task AMessageWithContactDetailsIsRefusedAndNotStored()
    {
        var talk = await NewTalkAsync();

        var refused = await SendMessageAsync(
            talk, talk.Client, "Call me on 09123456789 or mail me at a@b.com");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);

        var body = await ReadAsync(refused);

        Assert.Equal("contact_info", body.GetProperty("code").GetString());
        Assert.Contains(
            body.GetProperty("found").EnumerateArray(),
            kind => kind.GetString() == ContactInfoDetector.Phone);
        Assert.Contains(
            body.GetProperty("found").EnumerateArray(),
            kind => kind.GetString() == ContactInfoDetector.Email);

        var conversation = await GetConversationAsync(talk, talk.Client);

        Assert.Empty(conversation.GetProperty("messages").EnumerateArray());
    }

    // ---- who can talk ----------------------------------------------------

    [Fact]
    public async Task TheClientAndAnApplicantCanTalk()
    {
        var talk = await NewTalkAsync();

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendMessageAsync(
                talk, talk.Client, "Hello, can you start next week?")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendMessageAsync(
                talk, talk.Freelancer, "Yes, Monday works for me.")).StatusCode);

        var asClient = await GetConversationAsync(talk, talk.Client);
        var messages = asClient.GetProperty("messages").EnumerateArray().ToList();

        Assert.Equal(2, messages.Count);
        Assert.True(messages[0].GetProperty("mine").GetBoolean());
        Assert.False(messages[1].GetProperty("mine").GetBoolean());
        Assert.Equal(
            "Hello, can you start next week?",
            messages[0].GetProperty("body").GetString());

        Assert.Equal(
            "Freelancer",
            asClient.GetProperty("counterpart").GetProperty("role").GetString());
        Assert.True(asClient.GetProperty("canSend").GetBoolean());

        var asFreelancer = await GetConversationAsync(talk, talk.Freelancer);

        Assert.Equal(
            "Client",
            asFreelancer.GetProperty("counterpart").GetProperty("role").GetString());
        Assert.False(
            asFreelancer.GetProperty("messages")[0].GetProperty("mine").GetBoolean());
    }

    [Fact]
    public async Task StrangersHaveNoConversationToOpen()
    {
        var talk = await NewTalkAsync();

        await SendMessageAsync(talk, talk.Client, "Hello there.");

        var otherFreelancer = await NewPersonAsync(
            _client, "Freelancer", "Other Freelancer");

        var otherClient = await NewPersonAsync(_client, "Client", "Other Client");

        // Neither the other freelancer nor another client can read the
        // thread or write into it, by any combination of ids.
        foreach (var stranger in new[] { otherFreelancer, otherClient })
        {
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendAsync(
                    _client, HttpMethod.Get, ThreadUrl(talk), stranger.Token)).StatusCode);

            Assert.Equal(
                HttpStatusCode.NotFound,
                (await SendMessageAsync(talk, stranger, "Hi")).StatusCode);
        }

        // The other freelancer has not applied, so even the project's own
        // client has no conversation to open with them.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                _client,
                HttpMethod.Get,
                $"/api/projects/{talk.ProjectId}/chat/{otherFreelancer.Id}",
                talk.Client.Token)).StatusCode);

        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                _client,
                HttpMethod.Post,
                $"/api/projects/{talk.ProjectId}/chat/{otherFreelancer.Id}/messages",
                talk.Client.Token,
                new { Body = "Please apply." })).StatusCode);
    }

    [Fact]
    public async Task OneFreelancerNeverSeesWhatWasSaidToAnother()
    {
        var talk = await NewTalkAsync();

        var second = await NewPersonAsync(_client, "Freelancer", "Second Freelancer");

        await ApplyAsync(talk, second);

        await SendMessageAsync(talk, talk.Client, "Private note for the first one.");

        // The second applicant has a conversation, and it is empty.
        var secondView = await ReadAsync(await SendAsync(
            _client,
            HttpMethod.Get,
            $"/api/projects/{talk.ProjectId}/chat/{second.Id}",
            second.Token));

        Assert.Empty(secondView.GetProperty("messages").EnumerateArray());

        // And they cannot ask for the first one's thread.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await SendAsync(
                _client,
                HttpMethod.Get,
                ThreadUrl(talk),
                second.Token)).StatusCode);
    }

    [Fact]
    public async Task ThereIsNoWayToSendEmptyOrOversizedMessages()
    {
        var talk = await NewTalkAsync();

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendMessageAsync(talk, talk.Client, "   ")).StatusCode);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await SendMessageAsync(talk, talk.Client, new string('a', 2001))).StatusCode);

        Assert.Empty(
            (await GetConversationAsync(talk, talk.Client))
            .GetProperty("messages").EnumerateArray());
    }

    // ---- ending ----------------------------------------------------------

    [Fact]
    public async Task AFinishedProjectKeepsTheHistoryButTakesNoMoreMessages()
    {
        var talk = await NewTalkAsync();

        await SendMessageAsync(talk, talk.Client, "Welcome aboard.");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await database.Projects
                .Where(project => project.Id == talk.ProjectId)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    project => project.Status, ProjectStatus.Cancelled));
        }

        var conversation = await GetConversationAsync(talk, talk.Freelancer);

        Assert.False(conversation.GetProperty("canSend").GetBoolean());
        Assert.NotNull(conversation.GetProperty("cannotSendReason").GetString());
        Assert.Single(conversation.GetProperty("messages").EnumerateArray());

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await SendMessageAsync(talk, talk.Freelancer, "One more thing.")).StatusCode);
    }

    [Fact]
    public async Task AWithdrawnApplicantCanStillReadButNotWrite()
    {
        var talk = await NewTalkAsync();

        await SendMessageAsync(talk, talk.Client, "Can you do it by Friday?");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await database.Proposals
                .Where(proposal =>
                    proposal.ProjectId == talk.ProjectId &&
                    proposal.FreelancerId == talk.Freelancer.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(
                    proposal => proposal.Status, ProposalStatus.Withdrawn));
        }

        var asFreelancer = await GetConversationAsync(talk, talk.Freelancer);

        Assert.False(asFreelancer.GetProperty("canSend").GetBoolean());
        Assert.Single(asFreelancer.GetProperty("messages").EnumerateArray());

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await SendMessageAsync(talk, talk.Client, "Hello?")).StatusCode);
    }

    // ---- inbox, unread and polling ---------------------------------------

    [Fact]
    public async Task UnreadMessagesAreCountedAndClearedWhenOpened()
    {
        var talk = await NewTalkAsync();

        await SendMessageAsync(talk, talk.Client, "First question.");
        await SendMessageAsync(talk, talk.Client, "Second question.");

        var inbox = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/chat", talk.Freelancer.Token));

        Assert.Equal(2, inbox.GetProperty("unreadCount").GetInt32());

        var item = Assert.Single(inbox.GetProperty("items").EnumerateArray());

        Assert.Equal("Second question.", item.GetProperty("lastMessage").GetString());
        Assert.Equal(2, item.GetProperty("unreadCount").GetInt32());
        Assert.Equal(
            "Build a landing page",
            item.GetProperty("projectTitle").GetString());

        // The sender has nothing unread in their own thread.
        var senderInbox = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/chat", talk.Client.Token));

        Assert.Equal(0, senderInbox.GetProperty("unreadCount").GetInt32());

        // The notification bell asks for a reply.
        var notifications = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/notifications", talk.Freelancer.Token));

        Assert.Contains(
            notifications.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("kind").GetString() == "NewMessages" &&
                     entry.GetProperty("href").GetString() ==
                     $"/dashboard/messages/{talk.ProjectId}/{talk.Freelancer.Id}");

        await GetConversationAsync(talk, talk.Freelancer);

        var after = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/chat", talk.Freelancer.Token));

        Assert.Equal(0, after.GetProperty("unreadCount").GetInt32());

        var quiet = await ReadAsync(await SendAsync(
            _client, HttpMethod.Get, "/api/notifications", talk.Freelancer.Token));

        Assert.DoesNotContain(
            quiet.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("kind").GetString() == "NewMessages");
    }

    [Fact]
    public async Task PollingAsksOnlyForWhatIsNew()
    {
        var talk = await NewTalkAsync();

        var first = await ReadAsync(await SendMessageAsync(
            talk, talk.Client, "One."));

        var sentAt = first.GetProperty("sentAt").GetDateTimeOffset();

        await Task.Delay(20);

        await SendMessageAsync(talk, talk.Freelancer, "Two.");

        var newer = await ReadAsync(await SendAsync(
            _client,
            HttpMethod.Get,
            ThreadUrl(talk) + "?after=" + Uri.EscapeDataString(
                sentAt.ToString("O")),
            talk.Client.Token));

        var only = Assert.Single(newer.GetProperty("messages").EnumerateArray());

        Assert.Equal("Two.", only.GetProperty("body").GetString());
    }

    [Fact]
    public async Task AnAnonymousCallerIsRefused()
    {
        var talk = await NewTalkAsync();

        var response = await _client.GetAsync(ThreadUrl(talk));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await _client.GetAsync("/api/chat")).StatusCode);
    }

    [Fact]
    public async Task OlderPagesAndPolling_PreserveMessagesWithIdenticalTimestamps()
    {
        var talk = await NewTalkAsync();
        var first = await ReadAsync(await SendMessageAsync(talk, talk.Client, "Opening message."));
        List<Guid> expected;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var thread = await db.ChatThreads.SingleAsync(item => item.ProjectId == talk.ProjectId);
            var stamp = DateTimeOffset.UtcNow.AddMinutes(-1);
            var existing = await db.ChatMessages.SingleAsync(item => item.ThreadId == thread.Id);
            existing.SentAt = stamp;
            for (var index = 0; index < 419; index++)
                db.ChatMessages.Add(new ChatMessage
                {
                    ThreadId = thread.Id, SenderId = talk.Client.Id,
                    Body = "Older test message", SentAt = stamp
                });
            await db.SaveChangesAsync();
            expected = await db.ChatMessages.Where(item => item.ThreadId == thread.Id)
                .OrderBy(item => item.SentAt).ThenBy(item => item.Id).Select(item => item.Id).ToListAsync();
        }
        var latest = await GetConversationAsync(talk, talk.Freelancer);
        Assert.True(latest.GetProperty("hasOlderMessages").GetBoolean());
        var rows = latest.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(200, rows.Count);
        Assert.Equal(expected.Skip(220), rows.Select(item => item.GetProperty("id").GetGuid()));
        var all = rows.Select(item => item.GetProperty("id").GetGuid()).ToList();
        var firstId = all[0];
        for (var page = 0; page < 2; page++)
        {
            var response = await SendAsync(_client, HttpMethod.Get,
                ThreadUrl(talk) + $"?beforeMessageId={firstId}", talk.Freelancer.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var older = await ReadAsync(response);
            var ids = older.GetProperty("messages").EnumerateArray()
                .Select(item => item.GetProperty("id").GetGuid()).ToList();
            Assert.Equal(page == 0 ? 200 : 20, ids.Count);
            Assert.Equal(page == 0, older.GetProperty("hasOlderMessages").GetBoolean());
            all.InsertRange(0, ids);
            firstId = ids[0];
        }
        Assert.Equal(expected, all);
        Assert.Equal(420, all.Distinct().Count());
        var polling = await SendAsync(_client, HttpMethod.Get,
            ThreadUrl(talk) + $"?afterMessageId={expected[^2]}", talk.Freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, polling.StatusCode);
        var newest = Assert.Single((await ReadAsync(polling)).GetProperty("messages").EnumerateArray());
        Assert.Equal(expected[^1], newest.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task AMessageCursorFromAnotherConversationIsRefused()
    {
        var one = await NewTalkAsync();
        var two = await NewTalkAsync();
        await SendMessageAsync(one, one.Client, "First conversation.");
        var other = await ReadAsync(await SendMessageAsync(two, two.Client, "Other conversation."));
        foreach (var cursor in new[] { "beforeMessageId", "afterMessageId" })
        {
            var response = await SendAsync(_client, HttpMethod.Get,
                ThreadUrl(one) + $"?{cursor}={other.GetProperty("id").GetGuid()}", one.Client.Token);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        var invalid = await SendAsync(_client, HttpMethod.Get,
            ThreadUrl(one) + $"?beforeMessageId={Guid.NewGuid()}&afterMessageId={Guid.NewGuid()}", one.Client.Token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ---- helpers ---------------------------------------------------------

    private sealed record Talk(
        Person Client,
        Person Freelancer,
        Guid ProjectId,
        Guid MilestoneId);

    private static string ThreadUrl(Talk talk) =>
        $"/api/projects/{talk.ProjectId}/chat/{talk.Freelancer.Id}";

    // A client with a project, and a freelancer who has applied to it.
    private async Task<Talk> NewTalkAsync()
    {
        var client = await NewPersonAsync(_client, "Client", "Chat Client");
        var freelancer = await NewPersonAsync(_client, "Freelancer", "Chat Freelancer");

        var project = await ReadAsync(await SendAsync(
            _client, HttpMethod.Post, "/api/projects", client.Token,
            new
            {
                Title = "Build a landing page",
                Description = "A one page site with a contact form.",
                Budget = 1000m,
                Deadline = DateTimeOffset.UtcNow.AddDays(90)
            }));

        var projectId = project.GetProperty("id").GetGuid();

        var milestone = await ReadAsync(await SendAsync(
            _client, HttpMethod.Post,
            $"/api/projects/{projectId}/milestones", client.Token,
            new
            {
                Title = "Landing page",
                Description = "Responsive landing page with a working form.",
                Amount = 1000m,
                SequenceNumber = 1,
                Deadline = DateTimeOffset.UtcNow.AddDays(20)
            }));

        var talk = new Talk(
            client,
            freelancer,
            projectId,
            milestone.GetProperty("id").GetGuid());

        await ApplyAsync(talk, freelancer);

        return talk;
    }

    private async Task ApplyAsync(Talk talk, Person freelancer)
    {
        var response = await SendAsync(
            _client, HttpMethod.Post,
            $"/api/projects/{talk.ProjectId}/proposals", freelancer.Token,
            new
            {
                CoverLetter = "I can build this.",
                EstimatedDays = 14,
                MilestoneIds = new[] { talk.MilestoneId }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendMessageAsync(
        Talk talk,
        Person sender,
        string body) =>
        SendAsync(
            _client,
            HttpMethod.Post,
            ThreadUrl(talk) + "/messages",
            sender.Token,
            new { Body = body });

    private async Task<JsonElement> GetConversationAsync(Talk talk, Person viewer)
    {
        var response = await SendAsync(
            _client, HttpMethod.Get, ThreadUrl(talk), viewer.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadAsync(response);
    }
}
