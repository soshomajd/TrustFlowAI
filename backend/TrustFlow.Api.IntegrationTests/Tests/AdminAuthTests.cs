using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Identity;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private const string Login = "/api/auth/admin/login";
    private const string Setup = "/api/auth/admin/setup";

    public AdminAuthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = MakeClient(factory);
    }

    [Fact]
    public async Task VerificationReadsTheCodeLine_NotDigitsInTheAdministratorName()
    {
        var person = await NewPersonAsync(_client, "Client", "Administrator 123456");
        Assert.False(string.IsNullOrWhiteSpace(person.Token));
    }

    [Fact]
    public async Task ParticipantCredentialsAndUnknownAccounts_NeverReceiveAnAdminSession()
    {
        foreach (var role in new[] { "Client", "Freelancer" })
        {
            var person = await NewPersonAsync(_client, role, "Participant");
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var before = await db.RefreshTokens.CountAsync(token => token.UserId == person.Id);
            var response = await _client.PostAsJsonAsync(Login, new { person.Email, Password });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
            Assert.Equal(before, await db.RefreshTokens.CountAsync(token => token.UserId == person.Id));
            Assert.False(await db.AdminAccessLogs.AnyAsync(log => log.AdminId == person.Id));
        }
        var unknown = await _client.PostAsJsonAsync(Login, new { Email = $"missing-{Guid.NewGuid():N}@trustflow.test", Password });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.False(unknown.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ExistingAdmin_SignsInWithSecureCookie_AndCanReadAdminRecords()
    {
        var admin = await NewPersonAsync(_client, "Client", "Existing administrator");
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByIdAsync(admin.Id.ToString());
            Assert.True((await users.AddToRoleAsync(user!, AppRoles.Admin)).Succeeded);
        }
        var response = await TestAdminSignIn.CompleteAsync(_client, _factory.Services, admin.Email, Password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        foreach (var flag in new[] { "httponly", "secure", "samesite=lax", "path=/api/auth" })
            Assert.Contains(flag, cookie.ToLowerInvariant());
        var body = await ReadAsync(response);
        Assert.Contains(body.GetProperty("user").GetProperty("roles").EnumerateArray(), role => role.GetString() == "Admin");
        var token = body.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_client, HttpMethod.Get, "/api/admin/overview", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_client, HttpMethod.Get, "/api/admin/projects?pageSize=1", token)).StatusCode);
        using var auditScope = _factory.Services.CreateScope();
        var db = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.AdminAccessLogs.CountAsync(log => log.AdminId == admin.Id && log.Action == "AdminSignedIn"));
    }

    [Fact]
    public async Task SetupDeniesUnapprovedAddresses_AndCannotAcceptACallerChosenRole()
    {
        var email = $"unapproved-{Guid.NewGuid():N}@trustflow.test";
        var response = await _client.PostAsJsonAsync(Setup, new { FullName = "Unapproved", Email = email, Password, Role = "Admin" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task ApprovedAdministratorAddress_CannotRegisterThroughParticipantSignup()
    {
        var email = $"separate-admin-{Guid.NewGuid():N}@trustflow.test";
        using var configured = ForApproved(email);
        using var client = MakeClient(configured);
        foreach (var role in new[] { "Client", "Freelancer" })
        {
            var response = await client.PostAsJsonAsync("/api/auth/register", new { FullName = "Administrator", Email = email, Password, Role = role });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("admin_setup_required", (await ReadAsync(response)).GetProperty("code").GetString());
        }
        using var scope = configured.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await users.FindByEmailAsync(email));
    }

    [Fact]
    public async Task ApprovedSetup_CreatesOnlyAdminRole_RequiresEmailAndPreservesRefreshFlow()
    {
        var email = $"approved-{Guid.NewGuid():N}@trustflow.test";
        using var configured = ForApproved(email);
        using var client = MakeClient(configured);
        var setup = await client.PostAsJsonAsync(Setup, new { FullName = "Approved administrator", Email = email.ToUpperInvariant(), Password, Role = "Freelancer" });
        Assert.Equal(HttpStatusCode.Created, setup.StatusCode);
        Assert.False(setup.Headers.Contains("Set-Cookie"));
        var created = await ReadAsync(setup);
        Assert.Equal("Admin", created.GetProperty("role").GetString());
        Assert.True(created.GetProperty("verificationRequired").GetBoolean());
        using (var scope = configured.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            Assert.NotNull(user);
            Assert.False(user.EmailConfirmed);
            Assert.Equal("Admin", Assert.Single(await users.GetRolesAsync(user)));
        }
        var pending = await client.PostAsJsonAsync(Login, new { Email = email, Password });
        Assert.Equal(HttpStatusCode.Forbidden, pending.StatusCode);
        Assert.Equal("email_not_verified", (await ReadAsync(pending)).GetProperty("code").GetString());
        Assert.False(pending.Headers.Contains("Set-Cookie"));
        await TestAuth.ConfirmEmailAsync(client, email);
        var login = await TestAdminSignIn.CompleteAsync(client, configured.Services, email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await ReadAsync(login);
        Assert.Equal("Admin", Assert.Single(body.GetProperty("user").GetProperty("roles").EnumerateArray()).GetString());
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';')[0];
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", cookie);
        var refreshed = await client.SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.Equal("Admin", Assert.Single((await ReadAsync(refreshed)).GetProperty("user").GetProperty("roles").EnumerateArray()).GetString());
    }

    [Fact]
    public async Task SetupCannotOverwriteAnExistingAccountPasswordOrRoles()
    {
        var existing = await NewPersonAsync(_client, "Client", "Keep original");
        using var configured = ForApproved(existing.Email);
        using var client = MakeClient(configured);
        // Startup can legitimately grant the configured role; setup cannot
        // replace this account or its existing password.
        var response = await client.PostAsJsonAsync(Setup, new { FullName = "Overwrite", existing.Email, Password = "Different123" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Login, new { existing.Email, Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Login, new { existing.Email, Password = "Different123" })).StatusCode);
    }

    [Fact]
    public async Task WrongPasswordsAndLockedAdmins_CannotCreateRefreshTokens()
    {
        var email = $"locked-admin-{Guid.NewGuid():N}@trustflow.test";
        using var configured = ForApproved(email);
        using var client = MakeClient(configured);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Setup, new { FullName = "Lockout administrator", Email = email, Password })).StatusCode);
        await TestAuth.ConfirmEmailAsync(client, email);
        for (var i = 0; i < 6; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Login, new { Email = email, Password = "Wrongpass1" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(Login, new { Email = email, Password })).StatusCode);
        using var scope = configured.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(token => token.UserId == user.Id));
    }

    private WebApplicationFactory<Program> ForApproved(string email) => _factory.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Review:AdminEmails:0"] = email
        })));

    private static HttpClient MakeClient(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });
}
