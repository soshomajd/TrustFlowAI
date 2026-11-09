using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ProtectedDeliveryAccessTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string SourceLink = "https://example.test/private-originals/source.zip";
    private const string PrivateMarker = "PRIVATE-ORIGINAL-METADATA-DO-NOT-EXPOSE";
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });

    [Fact]
    public async Task UnpaidClientGetsOnlyRedactedMetadataAndAWatermarkedPixelPreview()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var original = CreateWhitePng(2400, 1800).Concat(Encoding.ASCII.GetBytes(PrivateMarker)).ToArray();
        var delivery = await SetSourceAndFileAsync(s, original);

        var dto = await GetDeliveryAsync(s, s.Client.Token);
        Assert.False(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.True(dto.GetProperty("hasSourceFiles").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dto.GetProperty("sourceFilesLink").ValueKind);
        Assert.True(dto.GetProperty("attachments")[0].GetProperty("hasPreview").GetBoolean());
        Assert.DoesNotContain(SourceLink, dto.GetRawText());
        Assert.False(dto.GetProperty("attachments")[0].TryGetProperty("content", out _));

        using var originalRequest = Request(HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        originalRequest.Headers.Range = new RangeHeaderValue(0, 7);
        using var denied = await client.SendAsync(originalRequest);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.DoesNotContain(PrivateMarker, await denied.Content.ReadAsStringAsync());

        using var previewRequest = Request(HttpMethod.Get, FilePath(s, delivery) + "/preview", s.Client.Token);
        previewRequest.Headers.Range = new RangeHeaderValue(0, 7);
        using var preview = await client.SendAsync(previewRequest);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        AssertPrivateFileHeaders(preview);
        var pixels = await preview.Content.ReadAsByteArrayAsync();
        Assert.False(original.SequenceEqual(pixels));
        Assert.DoesNotContain(PrivateMarker, Encoding.Latin1.GetString(pixels));
        using var bitmap = SKBitmap.Decode(pixels);
        Assert.NotNull(bitmap);
        Assert.InRange(bitmap.Width, 1, 1600);
        Assert.InRange(bitmap.Height, 1, 1600);
        // The fixture is entirely white, so re-encoding alone cannot pass this
        // assertion: the review image must contain visible protection marks.
        Assert.Contains(bitmap.Pixels, pixel => pixel.Red < 240 || pixel.Green < 240 || pixel.Blue < 240);

        var unchanged = await GetMilestoneAsync(s);
        Assert.Equal("Submitted", unchanged.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, unchanged.GetProperty("paidAmount").ValueKind);
    }

    [Fact]
    public async Task AuthorCanRetrieveTheirOriginalWhileClientEntitlementRemainsLocked()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var original = CreateWhitePng();
        var delivery = await SetSourceAndFileAsync(s, original);
        var dto = await GetDeliveryAsync(s, s.Freelancer.Token);
        Assert.False(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Equal(SourceLink, dto.GetProperty("sourceFilesLink").GetString());
        using var response = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(original, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        AssertPrivateFileHeaders(response);
    }

    [Fact]
    public async Task ResignationDoesNotGiveTheClientOrNewAssigneeThePreviousAuthorsOriginals()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var original = CreateWhitePng();
        var delivery = await SetSourceAndFileAsync(s, original);
        FakeEscrowChainDeployer.Resign(s.ContractAddress);
        await SyncAsync(s);
        var newAuthor = await NewPersonAsync(client, "Freelancer", "Replacement delivery author");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var milestone = await db.Milestones.SingleAsync(item => item.Id == s.MilestoneId);
            Assert.Null(milestone.FreelancerId);
            // This fixture models a later reassignment; access to an old file
            // must remain tied to its original submission author.
            milestone.FreelancerId = newAuthor.Id;
            await db.SaveChangesAsync();
        }
        Assert.False((await GetDeliveryAsync(s, s.Client.Token)).GetProperty("originalsUnlocked").GetBoolean());
        var authorHistory = await GetDeliveryAsync(s, s.Freelancer.Token);
        Assert.Equal(delivery.SubmissionId, authorHistory.GetProperty("id").GetGuid());
        using var authorDownload = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Freelancer.Token);
        Assert.Equal(HttpStatusCode.OK, authorDownload.StatusCode);
        Assert.Equal(original, await authorDownload.Content.ReadAsByteArrayAsync());
        using var clientDownload = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        Assert.Equal(HttpStatusCode.Forbidden, clientDownload.StatusCode);
        foreach (var suffix in new[] { "", "/preview" })
        {
            using var denied = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery) + suffix, newAuthor.Token);
            Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
        }
    }

    [Fact]
    public async Task OnlyConfirmedFullGrossReleaseUnlocksOriginalsDespiteTheTenPercentFee()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var original = CreateWhitePng();
        var delivery = await SetSourceAndFileAsync(s, original);
        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        // A chain event alone is insufficient until the verified release has
        // actually been reconciled and credited to this milestone.
        Assert.False((await GetDeliveryAsync(s, s.Client.Token)).GetProperty("originalsUnlocked").GetBoolean());
        using (var approved = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/approve", s.Client.Token))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);

        var dto = await GetDeliveryAsync(s, s.Client.Token);
        Assert.True(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Equal(SourceLink, dto.GetProperty("sourceFilesLink").GetString());
        using var downloaded = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
        Assert.Equal(original, await downloaded.Content.ReadAsByteArrayAsync());
        AssertPrivateFileHeaders(downloaded);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var milestone = await db.Milestones.AsNoTracking().SingleAsync(item => item.Id == s.MilestoneId);
        Assert.Equal(1000m, milestone.PaidAmount);
        Assert.Equal(100m, milestone.PlatformFeePaid);
        Assert.Equal(900m, milestone.PaidAmount - milestone.PlatformFeePaid);
        Assert.Single(await db.EscrowExecutionRecords.Where(item => item.MilestoneId == s.MilestoneId).ToListAsync());
    }

    [Fact]
    public async Task V3UsesItsFundedStageIndexRatherThanTheProjectSequenceToUnlockOriginals()
    {
        var owner = await NewPersonAsync(client, "Client", "Scoped delivery owner");
        var freelancer = await NewPersonAsync(client, "Freelancer", "Scoped delivery author");
        await ConnectWalletAsync(client, owner.Token);
        await ConnectWalletAsync(client, freelancer.Token);
        var project = await ReadAsync(await SendAsync(client, HttpMethod.Post, "/api/projects", owner.Token,
            new { Title = "Independent stage delivery", Description = "Only the second plan entry is assigned.",
                Budget = 200, Deadline = DateTimeOffset.UtcNow.AddDays(60) }));
        var projectId = project.GetProperty("id").GetGuid();
        Guid assignedId = Guid.Empty;
        for (var sequence = 1; sequence <= 2; sequence++)
        {
            var created = await ReadAsync(await SendAsync(client, HttpMethod.Post,
                $"/api/projects/{projectId}/milestones", owner.Token,
                new { Title = $"Plan stage {sequence}", Description = "Review protected source files.",
                    Amount = 100, SequenceNumber = sequence, Deadline = DateTimeOffset.UtcNow.AddDays(30) }));
            if (sequence == 2) assignedId = created.GetProperty("id").GetGuid();
        }
        var proposal = await ReadAsync(await SendAsync(client, HttpMethod.Post,
            $"/api/projects/{projectId}/proposals", freelancer.Token,
            new { CoverLetter = "I will deliver the second stage.", EstimatedDays = 3, MilestoneIds = new[] { assignedId } }));
        using (var selected = await SendAsync(client, HttpMethod.Patch,
                   $"/api/projects/{projectId}/proposals/{proposal.GetProperty("id").GetGuid()}/accept", owner.Token))
            Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using (var deployed = await SendAsync(client, HttpMethod.Post,
                   $"/api/projects/{projectId}/escrow/deploy", owner.Token))
            Assert.Equal(HttpStatusCode.OK, deployed.StatusCode);
        Escrow escrow;
        EscrowFundingStage stage;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == projectId);
            stage = await db.EscrowFundingStages.AsNoTracking().SingleAsync(item => item.EscrowId == escrow.Id);
        }
        Assert.Equal(0, stage.ChainIndex);
        FakeEscrowChainDeployer.FundStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash, 100_000_000);
        FakeEscrowChainDeployer.StartStage(escrow.ContractAddress!, 0, stage.FundingReference, stage.TermsHash);
        var s = new SubmittedMilestone(owner, freelancer, projectId, assignedId, escrow.ContractAddress!);
        await SyncAsync(s);
        await SubmitAsync(s, "Second plan stage, first contract stage", CreateWhitePng());
        var dto = await GetDeliveryAsync(s, owner.Token);
        Assert.False(dto.GetProperty("originalsUnlocked").GetBoolean());
        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        await SyncAsync(s);
        dto = await GetDeliveryAsync(s, owner.Token);
        Assert.True(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Equal(SourceLink, dto.GetProperty("sourceFilesLink").GetString());
        using var check = factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var paid = await database.Milestones.AsNoTracking().SingleAsync(item => item.Id == assignedId);
        Assert.Equal(2, paid.SequenceNumber);
        Assert.Equal(0, paid.OnChainIndex);
        Assert.Equal(100m, paid.PaidAmount);
        var record = await database.EscrowExecutionRecords.SingleAsync(item => item.MilestoneId == assignedId);
        record.MilestoneIndex = 1;
        await database.SaveChangesAsync();
        Assert.False((await GetDeliveryAsync(s, owner.Token)).GetProperty("originalsUnlocked").GetBoolean());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(200, false)]
    [InlineData(999.99, false)]
    [InlineData(1000, true)]
    public async Task ArbitrationUnlocksOnlyAFullGrossAward_NotARefundOrPartialAward(double gross, bool unlocked)
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var delivery = await SetSourceAndFileAsync(s, CreateWhitePng());
        FakeEscrowChainDeployer.SettleMilestone(s.ContractAddress, 0,
            TokenAmountConverter.ToBaseUnits((decimal)gross, 6), endEscrow: false);
        await SyncAsync(s);

        var dto = await GetDeliveryAsync(s, s.Client.Token);
        Assert.Equal(unlocked, dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Equal(unlocked ? SourceLink : null, dto.GetProperty("sourceFilesLink").GetString());
        using var response = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        Assert.Equal(unlocked ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = await db.EscrowExecutionRecords.AsNoTracking().SingleAsync(item => item.MilestoneId == s.MilestoneId);
        Assert.Equal("CaseSettlement", record.Kind);
    }

    [Fact]
    public async Task ApprovalAndPaidAmountWithoutACanonicalReleaseRemainLocked()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var delivery = await SetSourceAndFileAsync(s, CreateWhitePng());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Milestones.Where(item => item.Id == s.MilestoneId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, MileStoneStatus.Approved)
                .SetProperty(item => item.PaidAmount, 1000m));
        }
        Assert.False((await GetDeliveryAsync(s, s.Client.Token)).GetProperty("originalsUnlocked").GetBoolean());
        using var response = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("transaction")]
    [InlineData("block")]
    [InlineData("escrow")]
    [InlineData("sender")]
    [InlineData("kind")]
    [InlineData("index")]
    [InlineData("milestone")]
    public async Task MalformedOrMismatchedSavedPaymentEvidenceCannotUnlockOriginals(string fault)
    {
        var s = await CreateSubmittedMilestoneAsync(client, milestoneCount: fault == "milestone" ? 2 : 1);
        var delivery = await SetSourceAndFileAsync(s, CreateWhitePng());
        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        using (var approved = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/approve", s.Client.Token))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var record = await db.EscrowExecutionRecords.SingleAsync(item => item.MilestoneId == s.MilestoneId);
            switch (fault)
            {
                case "transaction": record.TransactionHash = "0x" + new string('z', 64); break;
                case "block": record.BlockHash = "0x" + new string('z', 64); break;
                case "escrow":
                    var otherProject = new Project { ClientId = s.Client.Id, Title = "Unrelated payment scope",
                        Description = "Its receipts cannot unlock this delivery.", Budget = 1000, Deadline = DateTimeOffset.UtcNow.AddDays(30) };
                    var otherEscrow = new Escrow { ProjectId = otherProject.Id, FreelancerId = s.Freelancer.Id,
                        ChainId = 31337, TokenAddress = "0x" + new string('1', 40), TotalAmount = 1000 };
                    db.Projects.Add(otherProject);
                    db.Escrows.Add(otherEscrow);
                    record.EscrowId = otherEscrow.Id;
                    break;
                case "sender": record.TransactionSender = "0x" + new string('7', 40); break;
                case "kind":
                    await db.EscrowExecutionRecords.Where(item => item.EscrowId == record.EscrowId && item.MilestoneIndex == -1).ExecuteDeleteAsync();
                    record.Kind = "WorkStarted";
                    record.MilestoneIndex = -1;
                    record.MilestoneId = null;
                    break;
                case "index": record.MilestoneIndex = 1; break;
                case "milestone": record.MilestoneId = s.AllMilestoneIds![1]; break;
            }
            await db.SaveChangesAsync();
        }
        var dto = await GetDeliveryAsync(s, s.Client.Token);
        Assert.False(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Null(dto.GetProperty("sourceFilesLink").GetString());
        using var denied = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery), s.Client.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task AFullPaymentForThePreviousMilestoneDoesNotUnlockAnUnpaidLaterDelivery()
    {
        var s = await CreateSubmittedMilestoneAsync(client, milestoneCount: 2);
        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        using (var approved = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/approve", s.Client.Token))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var later = s with { MilestoneId = s.AllMilestoneIds![1] };
        using (var started = await SendAsync(client, HttpMethod.Patch, MilestonePath(later) + "/start", s.Freelancer.Token))
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        await SubmitAsync(later, "Unpaid second stage", CreateWhitePng());
        var dto = await GetDeliveryAsync(later, s.Client.Token);
        Assert.False(dto.GetProperty("originalsUnlocked").GetBoolean());
        Assert.Null(dto.GetProperty("sourceFilesLink").GetString());
        var attachment = dto.GetProperty("attachments")[0];
        var delivery = new Delivery(dto.GetProperty("id").GetGuid(), attachment.GetProperty("id").GetGuid());
        using var denied = await SendAsync(client, HttpMethod.Get, FilePath(later, delivery), s.Client.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task RevisionHistoryKeepsEachVersionPrivateAndUnlocksBothAfterFullPayment()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var oldBytes = CreateWhitePng();
        var old = await SetSourceAndFileAsync(s, oldBytes);
        using (var revised = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/request-changes", s.Client.Token,
                   new { Reason = "Please change the heading and submit a second version." }))
            Assert.Equal(HttpStatusCode.OK, revised.StatusCode);
        using (var started = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/start", s.Freelancer.Token))
            Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        await SubmitAsync(s, "Revised heading", oldBytes);
        using var list = await SendAsync(client, HttpMethod.Get, ListPath(s), s.Client.Token);
        var versions = (await ReadAsync(list)).EnumerateArray().ToArray();
        Assert.Equal(2, versions.Length);
        Assert.Equal("Please change the heading and submit a second version.", versions[1].GetProperty("clientFeedback").GetString());
        foreach (var version in versions)
        {
            Assert.False(version.GetProperty("originalsUnlocked").GetBoolean());
            Assert.Null(version.GetProperty("sourceFilesLink").GetString());
            var file = version.GetProperty("attachments")[0].GetProperty("id").GetGuid();
            var d = new Delivery(version.GetProperty("id").GetGuid(), file);
            using var denied = await SendAsync(client, HttpMethod.Get, FilePath(s, d), s.Client.Token);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var preview = await SendAsync(client, HttpMethod.Get, FilePath(s, d) + "/preview", s.Client.Token);
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        }
        // Mixing the current submission with its previous version's file never
        // yields a preview or original, even to the authenticated project owner.
        var mismatch = old with { SubmissionId = versions[0].GetProperty("id").GetGuid() };
        foreach (var suffix in new[] { "", "/preview" })
        {
            using var denied = await SendAsync(client, HttpMethod.Get, FilePath(s, mismatch) + suffix, s.Client.Token);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        using (var approved = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/approve", s.Client.Token))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        foreach (var version in versions)
        {
            var file = version.GetProperty("attachments")[0].GetProperty("id").GetGuid();
            using var downloaded = await SendAsync(client, HttpMethod.Get,
                FilePath(s, new Delivery(version.GetProperty("id").GetGuid(), file)), s.Client.Token);
            Assert.Equal(HttpStatusCode.OK, downloaded.StatusCode);
            Assert.Equal(oldBytes, await downloaded.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task PreviewAndOriginalRequireTheRightProjectMilestoneVersionAndParty()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var delivery = await SetSourceAndFileAsync(s, CreateWhitePng());
        var stranger = await NewPersonAsync(client, "Client", "Unrelated delivery viewer");
        foreach (var suffix in new[] { "", "/preview" })
        {
            var url = FilePath(s, delivery) + suffix;
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
            using var unrelated = await SendAsync(client, HttpMethod.Get, url, stranger.Token);
            Assert.Contains(unrelated.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
            foreach (var wrong in new[]
                     {
                         s with { ProjectId = Guid.NewGuid() },
                         s with { MilestoneId = Guid.NewGuid() }
                     })
            {
                using var mismatched = await SendAsync(client, HttpMethod.Get, FilePath(wrong, delivery) + suffix, s.Client.Token);
                Assert.Equal(HttpStatusCode.NotFound, mismatched.StatusCode);
            }
        }
    }

    [Fact]
    public async Task LockedSourceUrlsCannotLeakThroughNotesDuplicatePreviewLinksCachedAiOrDisputeStatements()
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var delivery = await SetSourceAndFileAsync(s, CreateWhitePng());
        var statement = $"Please inspect the original work at {SourceLink} before deciding.";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.MilestoneSubmissions.SingleAsync(item => item.Id == delivery.SubmissionId);
            row.Note = statement;
            row.PreviewLink = SourceLink;
            await db.SaveChangesAsync();
        }
        var locked = await GetDeliveryAsync(s, s.Client.Token);
        Assert.DoesNotContain(SourceLink, locked.GetRawText());
        Assert.Null(locked.GetProperty("previewLink").GetString());
        var authorsCopy = await GetDeliveryAsync(s, s.Freelancer.Token);
        Assert.Equal(statement, authorsCopy.GetProperty("note").GetString());
        Assert.Equal(SourceLink, authorsCopy.GetProperty("previewLink").GetString());

        factory.AiModel.Reset();
        factory.AiModel.Handler = _ => Task.FromResult(new JsonObject
        {
            ["summary"] = $"Original files are at {SourceLink}",
            ["recommendation"] = "RequestChanges",
            ["confidence"] = 0.9,
            ["criteria"] = new JsonArray(new JsonObject
            {
                ["requirement"] = "The layout is correct.",
                ["status"] = "not_met",
                ["evidence"] = $"Compare the source at {SourceLink}"
            }),
            ["suggestedFeedback"] = $"Please update {SourceLink}"
        });
        var reviewPath = MilestonePath(s) + "/ai-review";
        try
        {
            using var generated = await SendAsync(client, HttpMethod.Post, reviewPath, s.Client.Token);
            Assert.Equal(HttpStatusCode.OK, generated.StatusCode);
            Assert.DoesNotContain(SourceLink, await generated.Content.ReadAsStringAsync());
            using var cached = await SendAsync(client, HttpMethod.Get, reviewPath, s.Client.Token);
            Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
            Assert.DoesNotContain(SourceLink, await cached.Content.ReadAsStringAsync());
            Assert.Equal(1, factory.AiModel.Calls);
        }
        finally { factory.AiModel.Reset(); }

        using var opened = await SendAsync(client, HttpMethod.Post, MilestonePath(s) + "/disputes", s.Freelancer.Token,
            new { Statement = statement });
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var disputeId = (await ReadAsync(opened)).GetProperty("id").GetGuid();
        using (var clientRead = await SendAsync(client, HttpMethod.Get, $"/api/disputes/{disputeId}", s.Client.Token))
        {
            Assert.Equal(HttpStatusCode.OK, clientRead.StatusCode);
            Assert.DoesNotContain(SourceLink, await clientRead.Content.ReadAsStringAsync());
        }
        using (var authorRead = await SendAsync(client, HttpMethod.Get, $"/api/disputes/{disputeId}", s.Freelancer.Token))
            Assert.Contains(SourceLink, await authorRead.Content.ReadAsStringAsync());
        using (var clientList = await SendAsync(client, HttpMethod.Get, MilestonePath(s) + "/disputes", s.Client.Token))
            Assert.DoesNotContain(SourceLink, await clientList.Content.ReadAsStringAsync());

        var admin = await NewPersonAsync(client, "Client", "Protected delivery administrator");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await users.FindByIdAsync(admin.Id.ToString()))!;
            Assert.True((await users.AddToRoleAsync(account, AppRoles.Admin)).Succeeded);
        }
        using (var login = await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password))
            admin = admin with { Token = (await ReadAsync(login)).GetProperty("accessToken").GetString()! };
        using (var evidence = await SendAsync(client, HttpMethod.Get, $"/api/admin/projects/{s.ProjectId}/deliveries", admin.Token))
        {
            Assert.Equal(HttpStatusCode.OK, evidence.StatusCode);
            Assert.Contains(SourceLink, await evidence.Content.ReadAsStringAsync());
        }
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.MilestoneSubmissions.AsNoTracking().SingleAsync(item => item.Id == delivery.SubmissionId);
            Assert.Equal(statement, stored.Note);
            Assert.Equal(SourceLink, stored.PreviewLink);
            Assert.Equal(SourceLink, stored.SourceFilesLink);
            Assert.Contains(SourceLink, (await db.MilestoneAiReviews.AsNoTracking()
                .SingleAsync(item => item.SubmissionId == delivery.SubmissionId)).ReviewJson);
            Assert.Equal(statement, (await db.Disputes.AsNoTracking().SingleAsync(item => item.Id == disputeId)).OpenerStatement);
        }

        FakeEscrowChainDeployer.MarkMilestoneReleased(s.ContractAddress, 0);
        using (var approved = await SendAsync(client, HttpMethod.Patch, MilestonePath(s) + "/approve", s.Client.Token))
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var purchased = await GetDeliveryAsync(s, s.Client.Token);
        Assert.Equal(statement, purchased.GetProperty("note").GetString());
        Assert.Equal(SourceLink, purchased.GetProperty("previewLink").GetString());
        using (var purchasedReview = await SendAsync(client, HttpMethod.Get, reviewPath, s.Client.Token))
            Assert.Contains(SourceLink, await purchasedReview.Content.ReadAsStringAsync());
        using (var purchasedDispute = await SendAsync(client, HttpMethod.Get, $"/api/disputes/{disputeId}", s.Client.Token))
            Assert.Contains(SourceLink, await purchasedDispute.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/png", false)]
    public async Task PdfOrUndecodableImageNeverReturnsOriginalBytesFromThePreviewRoute(string type, bool validPdf)
    {
        var s = await CreateSubmittedMilestoneAsync(client);
        var bytes = validPdf ? CreatePdf("First page for review") : Encoding.ASCII.GetBytes(PrivateMarker);
        var delivery = await SetSourceAndFileAsync(s, bytes, type);
        using var preview = await SendAsync(client, HttpMethod.Get, FilePath(s, delivery) + "/preview", s.Client.Token);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("image/png", preview.Content.Headers.ContentType!.MediaType);
        AssertPrivateFileHeaders(preview);
        var rendered = await preview.Content.ReadAsByteArrayAsync();
        Assert.False(bytes.SequenceEqual(rendered));
        Assert.DoesNotContain(PrivateMarker, Encoding.Latin1.GetString(rendered));
        using var bitmap = SKBitmap.Decode(rendered);
        Assert.NotNull(bitmap);
        Assert.InRange(bitmap.Width, 1, 1600);
        Assert.InRange(bitmap.Height, 1, 1600);
    }

    private async Task<Delivery> SetSourceAndFileAsync(SubmittedMilestone scenario, byte[] original, string contentType = "image/png")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var submission = await db.MilestoneSubmissions.Include(item => item.Attachments)
            .SingleAsync(item => item.MilestoneId == scenario.MilestoneId);
        submission.SourceFilesLink = SourceLink;
        var attachment = Assert.Single(submission.Attachments);
        attachment.Content = original;
        attachment.FileSize = original.Length;
        attachment.ContentType = contentType;
        attachment.OriginalFileName = contentType == "application/pdf" ? "private-original.pdf" : "private-original.png";
        attachment.Sha256 = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();
        await db.SaveChangesAsync();
        return new Delivery(submission.Id, attachment.Id);
    }

    private async Task<JsonElement> GetDeliveryAsync(SubmittedMilestone scenario, string token)
    {
        using var response = await SendAsync(client, HttpMethod.Get, ListPath(scenario), token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await ReadAsync(response)).EnumerateArray().First();
    }

    private async Task<JsonElement> GetMilestoneAsync(SubmittedMilestone s)
    {
        using var response = await SendAsync(client, HttpMethod.Get, MilestonePath(s), s.Client.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private async Task SyncAsync(SubmittedMilestone s)
    {
        using var response = await SendAsync(client, HttpMethod.Post,
            $"/api/projects/{s.ProjectId}/escrow/sync?freelancerId={s.Freelancer.Id}", s.Client.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SubmitAsync(SubmittedMilestone s, string note, byte[] bytes)
    {
        using var request = Request(HttpMethod.Post, ListPath(s), s.Freelancer.Token);
        var form = new MultipartFormDataContent
        {
            { new StringContent(note), "note" },
            { new StringContent("https://example.test/review"), "previewLink" },
            { new StringContent(SourceLink), "sourceFilesLink" }
        };
        var image = new ByteArrayContent(bytes);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "files", "delivery.png");
        request.Content = form;
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static void AssertPrivateFileHeaders(HttpResponseMessage response)
    {
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("sandbox", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    private static byte[] CreateWhitePng(int width = 800, int height = 600)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.White);
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static byte[] CreatePdf(string text)
    {
        var stream = $"BT /F1 12 Tf 72 720 Td ({text}) Tj ET\n";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}endstream"
        };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets) pdf.Append(offset.ToString("D10")).Append(" 00000 n \n");
        pdf.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    private static string MilestonePath(SubmittedMilestone s) =>
        $"/api/projects/{s.ProjectId}/milestones/{s.MilestoneId}";
    private static string ListPath(SubmittedMilestone s) => MilestonePath(s) + "/submissions";
    private static string FilePath(SubmittedMilestone s, Delivery d) =>
        ListPath(s) + $"/{d.SubmissionId}/files/{d.AttachmentId}";
    private sealed record Delivery(Guid SubmissionId, Guid AttachmentId);
}
