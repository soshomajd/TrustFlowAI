using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nethereum.Signer;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class WalletFlowTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Testpass1";

    private readonly HttpClient _client;

    public WalletFlowTests(
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
    public async Task WalletChallenge_CanBeSigned_AndVerified()
    {
        var accessToken =
            await RegisterAndLoginAsync();

        var walletKey =
            EthECKey.GenerateKey();

        var walletAddress =
            walletKey.GetPublicAddress();

        using var challengeRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/wallet/challenge",
                accessToken,
                new
                {
                    WalletAddress =
                        walletAddress
                }
            );

        var challengeResponse =
            await _client.SendAsync(
                challengeRequest
            );

        Assert.Equal(
            HttpStatusCode.OK,
            challengeResponse.StatusCode
        );

        var challenge =
            await ReadChallengeAsync(
                challengeResponse
            );

        Assert.Equal(
            walletAddress,
            challenge.WalletAddress,
            ignoreCase: true
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                challenge.Message
            )
        );

        Assert.True(
            challenge.ExpiresAt >
            DateTimeOffset.UtcNow
        );

        var signer =
            new EthereumMessageSigner();

        var signature =
            signer.EncodeUTF8AndSign(
                challenge.Message,
                walletKey
            );

        using var verifyRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/wallet/verify",
                accessToken,
                new
                {
                    Signature = signature
                }
            );

        var verifyResponse =
            await _client.SendAsync(
                verifyRequest
            );

        Assert.Equal(
            HttpStatusCode.OK,
            verifyResponse.StatusCode
        );

        await using var verifyStream =
            await verifyResponse.Content
                .ReadAsStreamAsync();

        using var verifyDocument =
            await JsonDocument.ParseAsync(
                verifyStream
            );

        var verifiedWalletAddress =
            verifyDocument.RootElement
                .GetProperty("walletAddress")
                .GetString();

        var verifiedAt =
            verifyDocument.RootElement
                .GetProperty("verifiedAt")
                .GetDateTimeOffset();

        Assert.Equal(
            walletAddress,
            verifiedWalletAddress,
            ignoreCase: true
        );

        Assert.True(
            verifiedAt <=
            DateTimeOffset.UtcNow
        );

        using var replayRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/wallet/verify",
                accessToken,
                new
                {
                    Signature = signature
                }
            );

        var replayResponse =
            await _client.SendAsync(
                replayRequest
            );

        Assert.Equal(
            HttpStatusCode.BadRequest,
            replayResponse.StatusCode
        );

        var replayBody =
            await replayResponse.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "No active wallet challenge",
            replayBody,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private async Task<string>
        RegisterAndLoginAsync()
    {
        var email =
            $"wallet-{Guid.NewGuid():N}" +
            "@trustflow.test";

        var registerResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName =
                        "Wallet Integration User",

                    Email = email,

                    Password,

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
                    Password
                }
            );

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode
        );

        await using var responseStream =
            await loginResponse.Content
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
            string.IsNullOrWhiteSpace(
                accessToken
            )
        );

        return accessToken!;
    }

    private static async Task<WalletChallenge>
        ReadChallengeAsync(
            HttpResponseMessage response)
    {
        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        return new WalletChallenge
        {
            WalletAddress =
                document.RootElement
                    .GetProperty(
                        "walletAddress"
                    )
                    .GetString()
                ?? string.Empty,

            Message =
                document.RootElement
                    .GetProperty("message")
                    .GetString()
                ?? string.Empty,

            ExpiresAt =
                document.RootElement
                    .GetProperty("expiresAt")
                    .GetDateTimeOffset()
        };
    }

    private static HttpRequestMessage
        CreateAuthorizedJsonRequest(
            HttpMethod method,
            string requestUri,
            string accessToken,
            object body)
    {
        var request =
            new HttpRequestMessage(
                method,
                requestUri
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        request.Content =
            JsonContent.Create(body);

        return request;
    }

    private sealed class WalletChallenge
    {
        public string WalletAddress
        {
            get;
            init;
        } = string.Empty;

        public string Message
        {
            get;
            init;
        } = string.Empty;

        public DateTimeOffset ExpiresAt
        {
            get;
            init;
        }
    }
}