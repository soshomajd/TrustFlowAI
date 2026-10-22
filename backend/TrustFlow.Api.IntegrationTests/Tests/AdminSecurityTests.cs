using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Auth;
using TrustFlow.Api.Services.Email;
using TrustFlow.Api.Services.Security;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AdminSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private const string Login = "/api/auth/admin/login";
    private const string Verify = "/api/auth/admin/mfa";
    public AdminSecurityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = Client(factory);
    }

    [Fact]
    public async Task PasswordAlone_NeverIssuesAdminTokens_AndCodeTravelsOnlyByEmail()
    {
        var admin = await AdminAsync();
        var messagesBefore = TestMailbox.For(admin.Email).Count;
        var response = await _client.PostAsJsonAsync(Login, new { admin.Email, Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.True(response.Headers.CacheControl?.NoStore);
        var prompt = await ReadAsync(response);
        Assert.True(prompt.GetProperty("mfaRequired").GetBoolean());
        Assert.False(prompt.TryGetProperty("accessToken", out _));
        Assert.False(prompt.TryGetProperty("code", out _));
        Assert.Equal(messagesBefore + 1, TestMailbox.For(admin.Email).Count);
        Assert.Contains("administrator sign-in", TestMailbox.For(admin.Email).Last().Subject);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var challenge = await db.AdminMfaChallenges.SingleAsync(item => item.UserId == admin.Id);
        Assert.NotEqual(prompt.GetProperty("challengeToken").GetString(), challenge.TokenHash);
        Assert.False(await db.RefreshTokens.AnyAsync(item => item.UserId == admin.Id && item.AdminMfaVerifiedAt != null));
        var normal = await _client.PostAsJsonAsync("/api/auth/login", new { admin.Email, Password });
        Assert.Equal(HttpStatusCode.Forbidden, normal.StatusCode);
        Assert.Equal("admin_signin_required", (await ReadAsync(normal)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task CorrectEmailCode_IsConsumedOnce_AndConcurrentRetriesIssueOnlyOneSession()
    {
        var admin = await AdminAsync();
        var prompt = await PromptAsync(admin);
        var body = new { ChallengeToken = prompt.GetProperty("challengeToken").GetString(), Code = TestMailbox.LatestCode(admin.Email) };
        var responses = await Task.WhenAll(_client.PostAsJsonAsync(Verify, body), _client.PostAsJsonAsync(Verify, body));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        var accepted = responses.Single(response => response.IsSuccessStatusCode);
        var session = await ReadAsync(accepted);
        var token = session.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(_client, HttpMethod.Get, "/api/admin/overview", token)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.RefreshTokens.CountAsync(item => item.UserId == admin.Id && item.AdminMfaVerifiedAt != null));
        Assert.True((await db.AuthCodes.SingleAsync(item => item.UserId == admin.Id && item.Purpose == AuthCodePurpose.AdminSignIn)).ConsumedAt.HasValue);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync(Verify, body)).StatusCode);
    }

    [Fact]
    public async Task FiveWrongCodes_ExhaustTheChallenge_EvenWhenTheNextCodeIsCorrect()
    {
        var admin = await AdminAsync();
        var prompt = await PromptAsync(admin);
        var correct = TestMailbox.LatestCode(admin.Email);
        var wrong = correct == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(prompt, wrong)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await VerifyAsync(prompt, correct)).StatusCode);
    }

    [Fact]
    public async Task ExpiredChallengeAndChangedSecurityStamp_CannotAuthorizeAdmin()
    {
        foreach (var changeStamp in new[] { false, true })
        {
            var admin = await AdminAsync();
            var prompt = await PromptAsync(admin);
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (changeStamp)
                {
                    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    Assert.True((await users.UpdateSecurityStampAsync((await users.FindByIdAsync(admin.Id.ToString()))!)).Succeeded);
                }
                else await db.AdminMfaChallenges.Where(item => item.UserId == admin.Id)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
            }
            var result = await VerifyAsync(prompt, TestMailbox.LatestCode(admin.Email));
            Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
            Assert.False(result.Headers.Contains("Set-Cookie"));
        }
    }

    [Fact]
    public async Task AdminTokensWithoutMfa_AndLegacyRefreshCookies_AreRejected()
    {
        var admin = await AdminAsync();
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(admin.Id.ToString()))!;
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var old = jwt.CreateToken(user, [AppRoles.Admin]);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(_client, HttpMethod.Get, "/api/admin/overview", old.AccessToken)).StatusCode);
        var refresh = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().Create(user.Id);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RefreshTokens.Add(refresh.RefreshToken);
        await db.SaveChangesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "trustflow_refresh_token=" + refresh.RawToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task AdminRefreshDoesNotExtendMfaBeyondItsAbsoluteLifetime()
    {
        var admin = await AdminAsync();
        var response = await TestAdminSignIn.CompleteAsync(_client, _factory.Services, admin.Email, Password);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.RefreshTokens.Where(item => item.UserId == admin.Id && item.AdminMfaVerifiedAt != null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AdminMfaVerifiedAt, DateTimeOffset.UtcNow.AddHours(-13)));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task ManagementEndpointsDenyUnapprovedPeers_AndForwardedHeadersCannotGrantAccess()
    {
        foreach (var path in new[] { "/api/admin/overview", "/api/admin/projects", "/api/review/cases", "/api/auth/admin/login", "/api/auth/admin/setup", "/api/auth/admin/mfa" })
        {
            using var request = new HttpRequestMessage(path.Contains("/auth/") ? HttpMethod.Post : HttpMethod.Get, path);
            request.Headers.Add("X-Test-Peer-IP", "203.0.113.10");
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
        }
        using var spoof = new HttpRequestMessage(HttpMethod.Get, "/api/admin/overview");
        spoof.Headers.Add("X-Forwarded-For", "127.0.0.1");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(spoof)).StatusCode);
        var person = await NewPersonAsync(_client, "Client", "Public participant");
        using var normal = Request(HttpMethod.Post, "/api/auth/login", person.Token, new { person.Email, Password });
        normal.Headers.Add("X-Test-Peer-IP", "203.0.113.10");
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(normal)).StatusCode);
    }

    [Fact]
    public async Task AdminSessionCannotUseSharedEndpointsOutsideThePrivateNetwork()
    {
        var admin = await AdminAsync();
        var response = await TestAdminSignIn.CompleteAsync(_client, _factory.Services, admin.Email, Password);
        var body = await ReadAsync(response);
        using var request = Request(HttpMethod.Get, "/api/notifications", body.GetProperty("accessToken").GetString()!);
        request.Headers.Add("X-Test-Peer-IP", "203.0.113.10");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task LogOnlyEmailCannotPretendToDeliverAnAdministratorCode()
    {
        var admin = await AdminAsync();
        using var logged = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailDelivery>();
            services.AddSingleton<IEmailDelivery, LogEmailDelivery>();
        }));
        using var client = Client(logged);
        var before = TestMailbox.For(admin.Email).Count;
        var response = await client.PostAsJsonAsync(Login, new { admin.Email, Password });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("admin_email_unavailable", (await ReadAsync(response)).GetProperty("code").GetString());
        Assert.False(response.Headers.Contains("Set-Cookie"));
        Assert.Equal(before, TestMailbox.For(admin.Email).Count);
    }

    [Fact]
    public void PrivateNetworkConfiguration_FailsClosed_AndSupportsExplicitCidrs()
    {
        var defaults = new AdminNetworkAccess(new ConfigurationBuilder().Build());
        var context = new DefaultHttpContext();
        Assert.False(defaults.IsAllowed(context));
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        Assert.True(defaults.IsAllowed(context));
        context.Connection.RemoteIpAddress = IPAddress.Parse("::1");
        Assert.True(defaults.IsAllowed(context));
        var configured = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminSecurity:AllowedNetworks:0"] = "10.42.0.0/16" }).Build();
        var policy = new AdminNetworkAccess(configured);
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.42.3.4");
        Assert.True(policy.IsAllowed(context));
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.43.3.4");
        Assert.False(policy.IsAllowed(context));
        foreach (var invalid in new[] { "not-a-cidr", "0.0.0.0/0", "::/0" })
            Assert.Throws<InvalidOperationException>(() => new AdminNetworkAccess(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AdminSecurity:AllowedNetworks:0"] = invalid }).Build()));
    }

    private async Task<Person> AdminAsync()
    {
        var person = await NewPersonAsync(_client, "Client", "Email administrator");
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(person.Id.ToString()))!, AppRoles.Admin)).Succeeded);
        return person;
    }
    private async Task<JsonElement> PromptAsync(Person admin)
    {
        var response = await _client.PostAsJsonAsync(Login, new { admin.Email, Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }
    private Task<HttpResponseMessage> VerifyAsync(JsonElement prompt, string code) => _client.PostAsJsonAsync(Verify,
        new { ChallengeToken = prompt.GetProperty("challengeToken").GetString(), Code = code });
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });
}
