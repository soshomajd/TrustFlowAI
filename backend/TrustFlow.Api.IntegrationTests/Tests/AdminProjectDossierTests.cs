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

public sealed class AdminProjectDossierTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    private static readonly string[] Sections = ["participants", "milestones", "proposals", "escrows"];
    private static string Url(Guid id, string? section = null) => $"/api/admin/projects/{id}" + (section is null ? "" : "/" + section);

    [Fact]
    public async Task AnonymousAndParticipantsCannotReadDossiersOrTheirSections()
    {
        var people = new[] { await NewPersonAsync(client, "Client", "Ordinary owner"), await NewPersonAsync(client, "Freelancer", "Ordinary talent") };
        foreach (var section in new string?[] { null }.Concat(Sections))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Url(Guid.NewGuid(), section))).StatusCode);
            foreach (var person in people)
                Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(client, HttpMethod.Get, Url(Guid.NewGuid(), section), person.Token)).StatusCode);
        }
    }

    [Fact]
    public async Task DossierIncludesExactProjectMetadataAndScopedCounts_WithoutSecretsOrFutureSections()
    {
        var s = await SeedAsync();
        var response = await SendAsync(client, HttpMethod.Get, Url(s.Project.Id) + "?adminId=" + s.Owner.Id, s.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        var body = await ReadAsync(response);
        Assert.Equal(s.Project.Id, body.GetProperty("id").GetGuid());
        Assert.Equal("9007199254740991.01", body.GetProperty("budgetExact").GetString());
        Assert.Equal("Private <project>", body.GetProperty("title").GetString());
        Assert.Equal(4, body.GetProperty("participantCount").GetInt32());
        Assert.Equal(3, body.GetProperty("milestoneCount").GetInt32());
        Assert.Equal(4, body.GetProperty("proposalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("escrowCount").GetInt32());
        foreach (var forbidden in new[] { "passwordHash", "securityStamp", "threads", "messages", "deliveries", "email", "refreshTokens" })
            Assert.False(body.TryGetProperty(forbidden, out _));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.AdminAccessLogs.SingleAsync(item => item.ProjectId == s.Project.Id && item.Action == "AdminProjectViewed");
        Assert.Equal(s.Admin.Id, log.AdminId);
    }

    [Fact]
    public async Task ParticipantsAreDeduplicatedIncludeHistoryAndRejectedApplicants_AndExcludeUnrelatedPeople()
    {
        var s = await SeedAsync();
        var rows = await AllPagesAsync(s, "participants", 4);
        Assert.Equal(new[] { s.Owner.Id, s.Current.Id, s.Former.Id, s.Applicant.Id }.Order(), rows.Select(item => item.GetProperty("id").GetGuid()).Order());
        var former = rows.Single(item => item.GetProperty("id").GetGuid() == s.Former.Id);
        Assert.True(former.GetProperty("hasEscrow").GetBoolean());
        Assert.False(former.GetProperty("currentFreelancer").GetBoolean());
        Assert.Equal("current-account-wallet", former.GetProperty("walletAddress").GetString());
        var applicant = rows.Single(item => item.GetProperty("id").GetGuid() == s.Applicant.Id);
        Assert.True(applicant.GetProperty("hasProposal").GetBoolean());
        Assert.False(applicant.GetProperty("hasEscrow").GetBoolean());
        Assert.DoesNotContain(rows, item => item.GetProperty("id").GetGuid() == s.Unrelated.Id);
        Assert.All(rows, row => { Assert.False(row.TryGetProperty("email", out _)); Assert.False(row.TryGetProperty("walletVerificationNonce", out _)); });
    }

    [Fact]
    public async Task EverySectionPagesStablyAndKeepsProjectBoundariesAndContractSnapshots()
    {
        var s = await SeedAsync();
        var stages = await AllPagesAsync(s, "milestones", 3);
        Assert.Equal(s.Stages.OrderBy(item => item.SequenceNumber).ThenBy(item => item.Id).Select(item => item.Id), stages.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Contains(stages, item => item.GetProperty("amountExact").GetString() == "0.01");
        Assert.Contains(stages, item => item.GetProperty("freelancerId").ValueKind == JsonValueKind.Null);
        var proposals = await AllPagesAsync(s, "proposals", 4);
        Assert.Equal(s.Proposals.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id).Select(item => item.Id), proposals.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Contains(proposals, item => item.GetProperty("status").GetString() == "Withdrawn");
        Assert.Contains(proposals, item => item.GetProperty("status").GetString() == "Rejected");
        Assert.Contains(proposals, item => item.GetProperty("bidAmountExact").GetString() == "9007199254740991.01");
        var escrows = await AllPagesAsync(s, "escrows", 2);
        var former = escrows.Single(item => item.GetProperty("freelancerId").GetGuid() == s.Former.Id);
        Assert.Equal("Cancelled", former.GetProperty("status").GetString());
        Assert.Equal("contract-snapshot-wallet", former.GetProperty("freelancerWalletAddress").GetString());
        Assert.Equal("9007199254740991.01", former.GetProperty("totalAmountExact").GetString());
        Assert.Equal(0, former.GetProperty("platformFeeBasisPoints").GetInt32());
        Assert.Contains(escrows, item => item.GetProperty("platformFeeBasisPoints").GetInt32() == 1000);
    }

    [Fact]
    public async Task MissingAndInvalidRequestsDoNotWriteSuccessfulAccessLogs()
    {
        var s = await SeedAsync();
        foreach (var path in new[] { Url(Guid.NewGuid()), Url(Guid.NewGuid(), "participants"), Url(s.Project.Id, "messages"),
            Url(s.Project.Id, "proposals") + "?page=0", Url(s.Project.Id, "escrows") + "?pageSize=101", Url(s.Project.Id,"milestones") + "?page=1000001" })
        {
            var response = await SendAsync(client, HttpMethod.Get, path, s.Admin.Token);
            Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound });
        }
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().AdminAccessLogs.AnyAsync(item => item.ProjectId == s.Project.Id));
    }

    [Fact]
    public async Task ReadingIsAuditedPerDisplayedRecordAndNeverChangesWorkflowOrMoney()
    {
        var s = await SeedAsync();
        foreach (var section in Sections)
        {
            var response = await GetAsync(s, section, "?pageSize=1&adminId=" + s.Owner.Id);
            var body = await ReadAsync(response);
            var id = Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logs = await db.AdminAccessLogs.Where(item => item.ProjectId == s.Project.Id && item.Action.EndsWith("Record")).ToListAsync();
            Assert.Contains(logs, item => item.ResourceId == id && item.AdminId == s.Admin.Id);
        }
        using var finalScope = factory.Services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(8, await finalDb.AdminAccessLogs.CountAsync(item => item.ProjectId == s.Project.Id));
        Assert.Equal(ProjectStatus.InProgress, (await finalDb.Projects.SingleAsync(item => item.Id == s.Project.Id)).Status);
        Assert.All(await finalDb.Escrows.Where(item => item.ProjectId == s.Project.Id).ToListAsync(), item => Assert.Equal(0m, item.ReleasedAmount));
        Assert.All(await finalDb.Milestones.Where(item => item.ProjectId == s.Project.Id).ToListAsync(), item => Assert.Null(item.PaidAmount));
    }

    [Fact]
    public async Task EmptyLegacyProjectWithNoClientStillHasHonestEmptySections()
    {
        var s = await SeedAsync();
        var empty = new Project { Title = "Legacy empty", Description = "No client saved", Budget = 1, Deadline = DateTimeOffset.UtcNow };
        using (var scope = factory.Services.CreateScope())
        { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Projects.Add(empty); await db.SaveChangesAsync(); }
        var body = await ReadAsync(await SendAsync(client, HttpMethod.Get, Url(empty.Id), s.Admin.Token));
        Assert.Equal("Former client", body.GetProperty("clientName").GetString());
        Assert.Equal(0, body.GetProperty("participantCount").GetInt32());
        foreach (var section in Sections)
        {
            var response = await SendAsync(client, HttpMethod.Get, Url(empty.Id, section), s.Admin.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty((await ReadAsync(response)).GetProperty("items").EnumerateArray());
        }
    }

    private async Task<HttpResponseMessage> GetAsync(Scenario s, string section, string query)
    {
        var response = await SendAsync(client, HttpMethod.Get, Url(s.Project.Id, section) + query, s.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return response;
    }
    private async Task<List<JsonElement>> AllPagesAsync(Scenario s, string section, int total)
    {
        var result = new List<JsonElement>();
        for (var page = 1; page <= total; page++)
        {
            var body = await ReadAsync(await GetAsync(s, section, $"?pageSize=1&page={page}"));
            Assert.Equal(total, body.GetProperty("totalItems").GetInt32());
            result.Add(Assert.Single(body.GetProperty("items").EnumerateArray()));
        }
        Assert.Equal(total, result.Select(item => item.GetProperty("id").GetGuid()).Distinct().Count());
        return result;
    }
    private async Task<Scenario> SeedAsync()
    {
        var admin = await NewPersonAsync(client, "Client", "Dossier admin");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        }
        var session = await TestAdminSignIn.CompleteAsync(client, factory.Services, admin.Email, Password);
        admin = admin with { Token = (await ReadAsync(session)).GetProperty("accessToken").GetString()! };
        var owner = await NewPersonAsync(client, "Client", "Owner");
        var current = await NewPersonAsync(client, "Freelancer", "Current");
        var former = await NewPersonAsync(client, "Freelancer", "Former");
        var applicant = await NewPersonAsync(client, "Freelancer", "Applicant");
        var unrelated = await NewPersonAsync(client, "Freelancer", "Unrelated");
        var now = DateTimeOffset.UtcNow;
        var project = new Project { ClientId = owner.Id, Title = "Private <project>", Description = "Private dossier description", Budget = 9007199254740991.01m, Deadline = now.AddDays(30), Status = ProjectStatus.InProgress };
        var other = new Project { ClientId = unrelated.Id, Title = "Other project", Description = "Never leak", Budget = 1m, Deadline = now };
        var stages = Enumerable.Range(1,3).Select(index => new MileStone { ProjectId = project.Id, Title = "Stage " + index, Amount = index == 3 ? 0.01m : 20m, SequenceNumber = index, Deadline = now, FreelancerId = index == 3 ? null : current.Id }).ToArray();
        var proposals = new[] {
            new Proposal { ProjectId = project.Id, FreelancerId = current.Id, Status = ProposalStatus.Accepted, BidAmount = 9007199254740991.01m, CoverLetter = "Private letter", EstimatedDays = 4, CreatedAt = now },
            new Proposal { ProjectId = project.Id, FreelancerId = former.Id, Status = ProposalStatus.Withdrawn, BidAmount = 50m, CoverLetter = "Prior offer", EstimatedDays = 4, CreatedAt = now },
            new Proposal { ProjectId = project.Id, FreelancerId = applicant.Id, Status = ProposalStatus.Rejected, BidAmount = 50m, CoverLetter = "Rejected offer", EstimatedDays = 4, CreatedAt = now },
            new Proposal { ProjectId = project.Id, FreelancerId = applicant.Id, Status = ProposalStatus.Withdrawn, BidAmount = 50m, CoverLetter = "Withdrawn retry", EstimatedDays = 4, CreatedAt = now }
        };
        using var seedScope = factory.Services.CreateScope();
        var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.SingleAsync(user => user.Id == former.Id)).WalletAddress = "current-account-wallet";
        db.Projects.AddRange(project, other); db.Milestones.AddRange(stages); db.Proposals.AddRange(proposals);
        db.ProposalMilestones.Add(new ProposalMilestone { ProposalId = proposals[0].Id, MilestoneId = stages[0].Id });
        db.Escrows.AddRange(
            new Escrow { ProjectId = project.Id, FreelancerId = former.Id, TotalAmount = 9007199254740991.01m, ChainId = 31337, TokenAddress = "token", FreelancerWalletAddress = "contract-snapshot-wallet", Status = EscrowStatus.Cancelled },
            new Escrow { ProjectId = project.Id, FreelancerId = current.Id, TotalAmount = 40m, ChainId = 31337, TokenAddress = "token", PlatformFeeBasisPoints = 1000, Status = EscrowStatus.Funded },
            new Escrow { ProjectId = other.Id, FreelancerId = unrelated.Id, TotalAmount = 1m, ChainId = 31337, TokenAddress = "hidden", Status = EscrowStatus.PendingDeployment });
        db.Milestones.Add(new MileStone { ProjectId = other.Id, Title = "Other stage", SequenceNumber = 1, Amount = 1, Deadline = now });
        db.Proposals.Add(new Proposal { ProjectId = other.Id, FreelancerId = unrelated.Id, BidAmount = 1m, EstimatedDays = 1 });
        await db.SaveChangesAsync();
        return new Scenario(admin, owner, current, former, applicant, unrelated, project, stages, proposals);
    }
    private sealed record Scenario(Person Admin, Person Owner, Person Current, Person Former, Person Applicant,
        Person Unrelated, Project Project, MileStone[] Stages, Proposal[] Proposals);
}
