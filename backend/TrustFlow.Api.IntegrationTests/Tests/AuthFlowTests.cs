using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class AuthFlowTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string RefreshTokenCookieName =
        "trustflow_refresh_token";

    private readonly HttpClient _client;

    public AuthFlowTests(
        CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress =
                    new Uri("https://localhost"),

                AllowAutoRedirect = false,

                HandleCookies = false
            }
        );
    }

    [Fact]
    public async Task AuthFlow_RotatesRefreshToken_AndRejectsOldToken()
    {
        var email =
            $"integration-{Guid.NewGuid():N}@trustflow.test";

        const string password = "Testpass1";

        var registerResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName = "Integration Test User",
                    Email = email,
                    Password = password,
                    Role = "Client"
                }
            );

        Assert.Equal(
            HttpStatusCode.Created,
            registerResponse.StatusCode
        );

        var loginResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    Email = email,
                    Password = password
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode
        );

        await AssertAccessTokenExistsAsync(
            loginResponse
        );

        var originalRefreshCookie =
            GetRefreshTokenCookie(loginResponse);

        Assert.False(
            string.IsNullOrWhiteSpace(
                originalRefreshCookie
            )
        );

        AssertRefreshCookieIsSecure(
            loginResponse
        );

        using var refreshRequest =
            CreateRefreshRequest(
                originalRefreshCookie
            );

        var refreshResponse =
            await _client.SendAsync(refreshRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            refreshResponse.StatusCode
        );

        await AssertAccessTokenExistsAsync(
            refreshResponse
        );

        var rotatedRefreshCookie =
            GetRefreshTokenCookie(refreshResponse);

        Assert.NotEqual(
            originalRefreshCookie,
            rotatedRefreshCookie
        );

        using var reuseRequest =
            CreateRefreshRequest(
                originalRefreshCookie
            );

        var reuseResponse =
            await _client.SendAsync(reuseRequest);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            reuseResponse.StatusCode
        );

        var responseBody =
            await reuseResponse.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "already been used or revoked",
            responseBody,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static HttpRequestMessage CreateRefreshRequest(
        string refreshCookie)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/refresh"
        );

        request.Headers.Add(
            "Cookie",
            refreshCookie
        );

        return request;
    }

    private static string GetRefreshTokenCookie(
        HttpResponseMessage response)
    {
        var setCookieHeaders =
            response.Headers.GetValues(
                "Set-Cookie"
            );

        var refreshTokenHeader =
            setCookieHeaders.First(header =>
                header.StartsWith(
                    $"{RefreshTokenCookieName}=",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        return refreshTokenHeader
            .Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries
            )[0];
    }

    private static void AssertRefreshCookieIsSecure(
        HttpResponseMessage response)
    {
        var setCookieHeaders =
            response.Headers.GetValues(
                "Set-Cookie"
            );

        var refreshTokenHeader =
            setCookieHeaders.First(header =>
                header.StartsWith(
                    $"{RefreshTokenCookieName}=",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        Assert.Contains(
            "httponly",
            refreshTokenHeader,
            StringComparison.OrdinalIgnoreCase
        );

        Assert.Contains(
            "secure",
            refreshTokenHeader,
            StringComparison.OrdinalIgnoreCase
        );

        Assert.Contains(
            "samesite=lax",
            refreshTokenHeader,
            StringComparison.OrdinalIgnoreCase
        );

        Assert.Contains(
            "path=/api/auth",
            refreshTokenHeader,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static async Task
        AssertAccessTokenExistsAsync(
            HttpResponseMessage response)
    {
        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var accessToken =
            document.RootElement
                .GetProperty("accessToken")
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(accessToken)
        );
    }
}