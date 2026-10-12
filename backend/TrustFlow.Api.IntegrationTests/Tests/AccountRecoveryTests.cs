using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AccountRecoveryTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Testpass1";

    private const string RefreshTokenCookieName =
        "trustflow_refresh_token";

    private readonly CustomWebApplicationFactory _factory;

    private readonly HttpClient _client;

    public AccountRecoveryTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;

        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = false
            });
    }

    [Fact]
    public async Task Register_MailsACode_AndSignInIsBlockedUntilVerified()
    {
        var email = await RegisterAsync();

        Assert.Single(TestMailbox.For(email));

        // The wrong password is still just a wrong password.
        var wrongPassword = await LoginAsync(email, "Wrongpass1");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        var blocked = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        var body = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("email_not_verified", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task VerifyEmail_WithTheMailedCode_AllowsSignIn_AndTheCodeWorksOnce()
    {
        var email = await RegisterAsync();
        var code = TestMailbox.LatestCode(email);

        var verified = await VerifyAsync(email, code);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var signedIn = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);

        var reused = await VerifyAsync(email, code);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_AWrongCodeFails_AndFiveWrongGuessesLockTheCodeOut()
    {
        var email = await RegisterAsync();
        var realCode = TestMailbox.LatestCode(email);
        var wrongCode = realCode == "000000" ? "111111" : "000000";

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var wrong = await VerifyAsync(email, wrongCode);
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        // Even the right code no longer works: the guesser used the
        // attempts up, and a new code is needed.
        var late = await VerifyAsync(email, realCode);
        Assert.Equal(HttpStatusCode.BadRequest, late.StatusCode);

        var body = await late.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("code_exhausted", body.GetProperty("code").GetString());

        var stillBlocked = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Forbidden, stillBlocked.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_RejectsAnExpiredCode()
    {
        var email = await RegisterAsync();
        var code = TestMailbox.LatestCode(email);

        await ExpireAllCodesAsync(email);

        var expired = await VerifyAsync(email, code);
        Assert.Equal(HttpStatusCode.BadRequest, expired.StatusCode);

        var stillBlocked = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Forbidden, stillBlocked.StatusCode);
    }

    [Fact]
    public async Task Resend_IsThrottled_ThenReplacesTheOldCode()
    {
        var email = await RegisterAsync();
        var firstCode = TestMailbox.LatestCode(email);

        var tooSoon = await _client.PostAsJsonAsync(
            "/api/auth/resend-verification", new { Email = email });
        Assert.Equal(HttpStatusCode.TooManyRequests, tooSoon.StatusCode);
        Assert.Single(TestMailbox.For(email));

        await BackdateCodesAsync(email);

        string secondCode;

        // A fresh code can in principle repeat by chance, so draw until it
        // differs rather than letting one in a million fail the suite.
        var attempts = 0;

        do
        {
            var resent = await _client.PostAsJsonAsync(
                "/api/auth/resend-verification", new { Email = email });
            Assert.Equal(HttpStatusCode.Accepted, resent.StatusCode);

            secondCode = TestMailbox.LatestCode(email);

            if (secondCode == firstCode)
            {
                await BackdateCodesAsync(email);
            }
        }
        while (secondCode == firstCode && ++attempts < 5);

        Assert.NotEqual(firstCode, secondCode);

        var oldCode = await VerifyAsync(email, firstCode);
        Assert.Equal(HttpStatusCode.BadRequest, oldCode.StatusCode);

        var newCode = await VerifyAsync(email, secondCode);
        Assert.Equal(HttpStatusCode.OK, newCode.StatusCode);
    }

    [Fact]
    public async Task Resend_ForAnUnknownAddress_LooksLikeSuccess_AndSendsNothing()
    {
        var email = $"nobody-{Guid.NewGuid():N}@trustflow.test";

        var response = await _client.PostAsJsonAsync(
            "/api/auth/resend-verification", new { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(TestMailbox.For(email));
    }

    [Fact]
    public async Task ForgotPassword_AnswersTheSameForKnownAndUnknownAddresses()
    {
        var known = await RegisterAndVerifyAsync();
        var unknown = $"nobody-{Guid.NewGuid():N}@trustflow.test";

        var knownResponse = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { Email = known });
        var unknownResponse = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { Email = unknown });

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        Assert.Equal(
            await knownResponse.Content.ReadAsStringAsync(),
            await unknownResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            2,
            TestMailbox.For(known).Count);

        Assert.Empty(TestMailbox.For(unknown));
    }

    [Fact]
    public async Task ResetPassword_ChangesThePassword_AndSignsEveryOldSessionOut()
    {
        var email = await RegisterAndVerifyAsync();

        var firstLogin = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        var oldSession = GetRefreshCookie(firstLogin);

        await ForgotAsync(email);
        var code = TestMailbox.LatestCode(email);

        var reset = await ResetAsync(email, code, "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldPassword = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);

        var newPassword = await LoginAsync(email, "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, newPassword.StatusCode);

        using var refresh = new HttpRequestMessage(
            HttpMethod.Post, "/api/auth/refresh");
        refresh.Headers.Add("Cookie", oldSession);

        var refreshed = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);

        // The code is spent.
        var again = await ResetAsync(email, code, "Another1x");
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_AWeakPassword_DoesNotUseUpTheCode()
    {
        var email = await RegisterAndVerifyAsync();

        await ForgotAsync(email);
        var code = TestMailbox.LatestCode(email);

        var weak = await ResetAsync(email, code, "short");
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var noDigit = await ResetAsync(email, code, "nodigitshere");
        Assert.Equal(HttpStatusCode.BadRequest, noDigit.StatusCode);

        // The same code still works once the password is acceptable.
        var strong = await ResetAsync(email, code, "Strongpass1");
        Assert.Equal(HttpStatusCode.OK, strong.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithAWrongCode_ChangesNothing()
    {
        var email = await RegisterAndVerifyAsync();

        await ForgotAsync(email);
        var realCode = TestMailbox.LatestCode(email);
        var wrongCode = realCode == "000000" ? "111111" : "000000";

        var wrong = await ResetAsync(email, wrongCode, "Brandnew1");
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var stillOld = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, stillOld.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_ForAnUnverifiedAccount_AlsoVerifiesIt()
    {
        var email = await RegisterAsync();

        var blocked = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        await ForgotAsync(email);

        var reset = await ResetAsync(
            email, TestMailbox.LatestCode(email), "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var signedIn = await LoginAsync(email, "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_UnlocksAnAccountLockedByFailedSignIns()
    {
        var email = await RegisterAndVerifyAsync();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await LoginAsync(email, "Wrongpass1");
        }

        var locked = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);

        await ForgotAsync(email);

        var reset = await ResetAsync(
            email, TestMailbox.LatestCode(email), "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var signedIn = await LoginAsync(email, "Brandnew1");
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
    }

    [Fact]
    public async Task Refresh_RefusesASessionWhoseEmailIsNotVerified()
    {
        var email = await RegisterAndVerifyAsync();

        var login = await LoginAsync(email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = GetRefreshCookie(login);

        // A session that predates the rule: the account is unverified but
        // still holds a live refresh token.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            await database.Users
                .Where(user => user.Email == email)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    user => user.EmailConfirmed, false));
        }

        using var refresh = new HttpRequestMessage(
            HttpMethod.Post, "/api/auth/refresh");
        refresh.Headers.Add("Cookie", session);

        var refreshed = await _client.SendAsync(refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);
    }

    private async Task<string> RegisterAsync()
    {
        var email = $"recovery-{Guid.NewGuid():N}@trustflow.test";

        var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                FullName = "Recovery Test User",
                Email = email,
                Password,
                Role = "Client"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("verificationRequired").GetBoolean());

        return email;
    }

    private async Task<string> RegisterAndVerifyAsync()
    {
        var email = await RegisterAsync();

        await TestAuth.ConfirmEmailAsync(_client, email);

        return email;
    }

    private Task<HttpResponseMessage> LoginAsync(
        string email,
        string password) =>
        _client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = email, Password = password });

    private Task<HttpResponseMessage> VerifyAsync(
        string email,
        string code) =>
        _client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new { Email = email, Code = code });

    private async Task ForgotAsync(string email)
    {
        // A reset code has its own cooldown, separate from verification.
        var response = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { Email = email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private Task<HttpResponseMessage> ResetAsync(
        string email,
        string code,
        string newPassword) =>
        _client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new { Email = email, Code = code, NewPassword = newPassword });

    private static string GetRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").First(
            value => value.StartsWith(
                $"{RefreshTokenCookieName}=",
                StringComparison.OrdinalIgnoreCase));

        return header.Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private async Task ExpireAllCodesAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var database = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var userId = await database.Users
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .SingleAsync();

        await database.AuthCodes
            .Where(code => code.UserId == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                code => code.ExpiresAt,
                DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    // Moves every code for the account two minutes into the past, which
    // clears the resend cooldown without waiting for it.
    private async Task BackdateCodesAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var database = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var userId = await database.Users
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .SingleAsync();

        await database.AuthCodes
            .Where(code => code.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(code => code.CreatedAt,
                    code => code.CreatedAt.AddMinutes(-2)));
    }
}
