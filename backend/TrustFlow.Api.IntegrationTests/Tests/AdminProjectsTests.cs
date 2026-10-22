using System.Net;
using System.Net.Http.Json;
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

public sealed class AdminProjectsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private const string Url = "/api/admin/projects";

    public AdminProjectsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
    }

    [Fact]
    public async Task OrdinaryRolesAndAnonymous_CannotListOrAuditProjects()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Url)).StatusCode);
        foreach (var role in new[] { "Client", "Freelancer" })
        {
            var person = await NewPersonAsync(_client, role, "Ordinary participant");
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(_client, HttpMethod.Get, Url, person.Token)).StatusCode);
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.AdminId == person.Id));
        }
    }

    [Fact]
    public async Task PagedList_IsStableExactPrivateAndAuditsOnlyDisplayedRows()
    {
        var scenario = await SeedAsync();
        var first = await GetAsync(scenario, "&pageSize=2");
        Assert.Equal(4, first.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, first.GetProperty("totalPages").GetInt32());
        Assert.False(first.GetProperty("hasPreviousPage").GetBoolean());
        Assert.True(first.GetProperty("hasNextPage").GetBoolean());
        var second = await GetAsync(scenario, "&pageSize=2&page=2");
        Assert.True(second.GetProperty("hasPreviousPage").GetBoolean());
        Assert.False(second.GetProperty("hasNextPage").GetBoolean());
        var all = first.GetProperty("items").EnumerateArray().Concat(second.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(scenario.Projects.OrderBy(row => row.Id).Select(row => row.Id), all.Select(row => row.GetProperty("id").GetGuid()));
        Assert.Equal(4, all.Select(row => row.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.All(all, row => Assert.Equal("9007199254740991.01", row.GetProperty("budgetExact").GetString()));
        var raw = first.GetRawText() + second.GetRawText();
        foreach (var excluded in new[] { "private delivery", "hidden cover letter", scenario.Owner.Email, "walletAddress", "accessToken", "description" })
            Assert.DoesNotContain(excluded, raw);
        var empty = await GetAsync(scenario, "&page=999&pageSize=2");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AdminAccessLogs.Where(log => log.AdminId == scenario.Admin.Id).ToListAsync();
        Assert.Equal(3, logs.Count(log => log.Action == "AdminProjectsListed" && log.ProjectId == null));
        Assert.Equal(4, logs.Count(log => log.Action == "AdminProjectListed"));
        Assert.All(logs, log => Assert.Null(log.ResourceId));
        Assert.All(scenario.Projects, project => Assert.Contains(logs, log => log.ProjectId == project.Id));
        var saved = await db.Projects.Where(project => project.ClientId == scenario.Owner.Id).ToListAsync();
        Assert.All(saved, project =>
        {
            Assert.Equal(9007199254740991.01m, project.Budget);
            Assert.Equal(scenario.Projects.Single(original => original.Id == project.Id).Status, project.Status);
        });
        var escrow = await db.Escrows.SingleAsync(item => item.ProjectId == scenario.Projects[0].Id);
        Assert.Equal(EscrowStatus.Cancelled, escrow.Status);
        Assert.Equal(0m, escrow.ReleasedAmount);
        Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.AdminId == scenario.Owner.Id));
    }

    [Fact]
    public async Task Filters_IncludeEveryProjectStatusAndAcceptExactProjectId()
    {
        var scenario = await SeedAsync();
        foreach (var project in scenario.Projects)
        {
            var result = await GetAsync(scenario, "&status=" + project.Status);
            var row = Assert.Single(result.GetProperty("items").EnumerateArray());
            Assert.Equal(project.Id, row.GetProperty("id").GetGuid());
            var byId = await GetAsync(scenario, "&search=" + project.Id);
            Assert.Equal(project.Id, Assert.Single(byId.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
        var noMatch = await GetAsync(scenario, "&clientId=" + Guid.NewGuid(), includeOwner: false);
        Assert.Equal(0, noMatch.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task CurrentAndHistoricalFreelancers_AreSearchableDistinctAndRemainVisible()
    {
        var scenario = await SeedAsync();
        var project = scenario.Projects[0];
        var all = await GetAsync(scenario, "&search=" + project.Id);
        var row = Assert.Single(all.GetProperty("items").EnumerateArray());
        Assert.Equal(2, row.GetProperty("freelancers").GetArrayLength());
        Assert.Equal(2, row.GetProperty("milestoneCount").GetInt32());
        Assert.Equal(1, row.GetProperty("escrowCount").GetInt32());
        Assert.Equal(1, row.GetProperty("proposalCount").GetInt32());
        Assert.Equal(1, row.GetProperty("openCaseCount").GetInt32());
        foreach (var person in new[] { scenario.Current, scenario.Former })
        {
            var filtered = await GetAsync(scenario, "&freelancerId=" + person.Id);
            Assert.Equal(project.Id, Assert.Single(filtered.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            var named = await GetAsync(scenario, "&search=" + Uri.EscapeDataString(person.FullName.ToUpperInvariant()));
            Assert.Equal(project.Id, Assert.Single(named.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
        // A client-name search is also server-side, without exposing their email.
        Assert.Equal(4, (await GetAsync(scenario, "&search=" + Uri.EscapeDataString(scenario.Owner.FullName))).GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task LegacyProjectWithNoClient_IsStillVisibleToAdmin()
    {
        var scenario = await SeedAsync();
        var project = new Project { Title = "Legacy project", Description = "Private legacy contents",
            Budget = 50m, Deadline = DateTimeOffset.UtcNow.AddDays(30) };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }
        var result = await GetAsync(scenario, "&search=" + project.Id, includeOwner: false);
        var row = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal(project.Id, row.GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("clientId").ValueKind);
        Assert.Equal("Former client", row.GetProperty("clientName").GetString());
        Assert.Empty(row.GetProperty("freelancers").EnumerateArray());
    }

    [Fact]
    public async Task SearchEscapesWildcards_AndDateBoundsAreInclusiveFromExclusiveTo()
    {
        var scenario = await SeedAsync();
        var literal = await GetAsync(scenario, "&search=" + Uri.EscapeDataString("a%_\\z"));
        Assert.Equal(scenario.Projects[0].Id, Assert.Single(literal.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        var timestamp = scenario.Projects[0].CreatedAt;
        var from = Uri.EscapeDataString(timestamp.ToString("O"));
        var to = Uri.EscapeDataString(timestamp.AddDays(1).ToString("O"));
        Assert.Equal(4, (await GetAsync(scenario, "&from=" + from + "&to=" + to)).GetProperty("totalItems").GetInt32());
        Assert.Equal(0, (await GetAsync(scenario, "&to=" + from)).GetProperty("totalItems").GetInt32());
        Assert.Equal(0, (await GetAsync(scenario, "&from=" + to)).GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task InvalidFiltersAndPaging_AreRejectedBeforeAnyAuditWrite()
    {
        var admin = await NewAdminAsync();
        foreach (var suffix in new[] { "page=0", "page=1000001", "pageSize=0", "pageSize=101", "search=ab", "search=" + new string('x', 201), "status=999", "status=unknown", "clientId=invalid", "freelancerId=invalid", "from=bad", "from=2026-12-01&to=2026-01-01" })
        {
            var response = await SendAsync(_client, HttpMethod.Get, Url + "?" + suffix, admin.Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.AdminId == admin.Id && log.Action.StartsWith("AdminProject")));
    }

    private async Task<JsonElement> GetAsync(Scenario scenario, string suffix, bool includeOwner = true)
    {
        var response = await SendAsync(_client, HttpMethod.Get,
            Url + "?adminId=" + scenario.Owner.Id + (includeOwner ? "&clientId=" + scenario.Owner.Id : "") + suffix, scenario.Admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return await ReadAsync(response);
    }

    private async Task<Person> NewAdminAsync()
    {
        var admin = await NewPersonAsync(_client, "Client", "Test list administrator");
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(admin.Id.ToString());
            Assert.True((await users.AddToRoleAsync(user!, AppRoles.Admin)).Succeeded);
        }
        var login = await TestAdminSignIn.CompleteAsync(_client, _factory.Services, admin.Email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return admin with { Token = (await ReadAsync(login)).GetProperty("accessToken").GetString()! };
    }

    private async Task<Scenario> SeedAsync()
    {
        var admin = await NewAdminAsync();
        var unique = Guid.NewGuid().ToString("N");
        var owner = await NewPersonAsync(_client, "Client", "List owner " + unique);
        var current = await NewPersonAsync(_client, "Freelancer", "Current " + unique);
        var former = await NewPersonAsync(_client, "Freelancer", "Historical " + unique);
        var timestamp = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
        var projects = Enum.GetValues<ProjectStatus>().Select(status => new Project
        {
            ClientId = owner.Id, Title = status == ProjectStatus.Open ? "Literal a%_\\z project" : "Other " + status,
            Description = "private delivery", Budget = 9007199254740991.01m,
            Status = status, CreatedAt = timestamp, Deadline = timestamp.AddDays(30)
        }).ToArray();
        var stage = new MileStone { ProjectId = projects[0].Id, Title = "Stage", Amount = 50m, SequenceNumber = 1, Deadline = timestamp.AddDays(30), FreelancerId = current.Id };
        var another = new MileStone { ProjectId = projects[0].Id, Title = "Another", Amount = 50m, SequenceNumber = 2, Deadline = timestamp.AddDays(30), FreelancerId = current.Id };
        var escrow = new Escrow { ProjectId = projects[0].Id, FreelancerId = former.Id, TotalAmount = 100m, ChainId = 31337, TokenAddress = "0x1111111111111111111111111111111111111111", Status = EscrowStatus.Cancelled };
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Projects.AddRange(projects);
        db.Milestones.AddRange(stage, another);
        db.Escrows.Add(escrow);
        db.Proposals.Add(new Proposal { ProjectId = projects[0].Id, FreelancerId = current.Id, CoverLetter = "hidden cover letter", BidAmount = 100m, EstimatedDays = 3 });
        db.EscrowCases.Add(new EscrowCase { EscrowId = escrow.Id, MilestoneId = stage.Id, OpenedAt = timestamp, Status = EscrowCaseStatus.Open });
        await db.SaveChangesAsync();
        return new Scenario(admin, owner, current, former, projects);
    }

    private sealed record Scenario(Person Admin, Person Owner, Person Current, Person Former, Project[] Projects);
}
