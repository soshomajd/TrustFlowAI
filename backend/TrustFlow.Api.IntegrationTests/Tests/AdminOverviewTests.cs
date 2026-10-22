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

public sealed class AdminOverviewTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private const string Url = "/api/admin/overview";

    public AdminOverviewTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
        });
    }

    [Fact]
    public async Task AnonymousAndOrdinaryRoles_CannotReadOverviewOrCreateAccessRecords()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync(Url)).StatusCode);
        var client = await NewPersonAsync(_client, "Client", "Ordinary client");
        var freelancer = await NewPersonAsync(_client, "Freelancer", "Ordinary freelancer");
        foreach (var person in new[] { client, freelancer })
            Assert.Equal(HttpStatusCode.Forbidden,
                (await SendAsync(_client, HttpMethod.Get, Url, person.Token)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.AdminAccessLogs.AnyAsync(item => item.AdminId == client.Id || item.AdminId == freelancer.Id));
    }

    [Fact]
    public async Task AdminSeesAllProjectStates_AndOnlyAggregateData_WithDurableAudit()
    {
        var admin = await NewAdminAsync();
        var owner = await NewPersonAsync(_client, "Client", "Independent project owner");
        var rows = Enum.GetValues<ProjectStatus>().Select(status => new Project
        {
            ClientId = owner.Id, Title = "Private title must not appear", Description = "Private description",
            Status = status, Budget = 123.45m, Deadline = DateTimeOffset.UtcNow.AddDays(30)
        }).ToArray();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Projects.AddRange(rows);
            await db.SaveChangesAsync();
        }

        var before = DateTimeOffset.UtcNow;
        var response = await SendAsync(_client, HttpMethod.Get, Url + "?adminId=" + owner.Id, admin.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.True(response.Headers.CacheControl?.Private);
        var body = await ReadAsync(response);
        var fields = new[] { "openProjects", "inProgressProjects", "completedProjects", "cancelledProjects" };
        Assert.All(fields, field => Assert.True(body.GetProperty(field).GetInt32() >= 1));
        Assert.Equal(body.GetProperty("totalProjects").GetInt32(), fields.Sum(field => body.GetProperty(field).GetInt32()));
        Assert.True(body.GetProperty("generatedAt").GetDateTimeOffset() >= before);
        Assert.Equal(8, body.EnumerateObject().Count());
        Assert.DoesNotContain("Private title", body.GetRawText());
        Assert.DoesNotContain(owner.Email, body.GetRawText());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.AdminAccessLogs.SingleAsync(item => item.AdminId == admin.Id && item.Action == "AdminOverviewViewed");
            Assert.Equal("AdminOverviewViewed", log.Action);
            Assert.True(log.CreatedAt >= before);
            Assert.Null(log.ProjectId);
            Assert.Null(log.ResourceId);
            var saved = await db.Projects.Where(item => rows.Select(row => row.Id).Contains(item.Id)).ToListAsync();
            Assert.Equal(rows.Length, saved.Count);
            foreach (var row in rows)
            {
                var actual = saved.Single(item => item.Id == row.Id);
                Assert.Equal(row.Status, actual.Status);
                Assert.Equal(123.45m, actual.Budget);
            }
        }
    }

    [Fact]
    public async Task ExplicitRefreshRecordsEachAccess_AndDoesNotExposeAuditDetails()
    {
        var admin = await NewAdminAsync();
        for (var i = 0; i < 2; i++)
        {
            var response = await SendAsync(_client, HttpMethod.Get, Url, admin.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(admin.Id.ToString(), await response.Content.ReadAsStringAsync());
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.AdminAccessLogs.CountAsync(item => item.AdminId == admin.Id && item.Action == "AdminOverviewViewed"));
    }

    [Fact]
    public async Task RegistrationCannotRequestAdminPrivileges()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            FullName = "Self appointed admin", Email = $"no-admin-{Guid.NewGuid():N}@trustflow.test",
            Password, Role = "Admin"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Person> NewAdminAsync()
    {
        var person = await NewPersonAsync(_client, "Client", "Test administrator");
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(person.Id.ToString());
            Assert.True((await users.AddToRoleAsync(user!, AppRoles.Admin)).Succeeded);
        }
        var login = await TestAdminSignIn.CompleteAsync(_client, _factory.Services, person.Email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync(login);
        return person with { Token = body.GetProperty("accessToken").GetString()! };
    }
}
