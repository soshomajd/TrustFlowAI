using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class DecimalValidationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DecimalValidationTests(
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
    public async Task CreateProject_WithUnsupportedDecimalValues_ReturnsBadRequest()
    {
        var email =
            $"decimal-client-{Guid.NewGuid():N}@trustflow.test";

        const string password = "Testpass1";

        await RegisterClientAsync(
            email,
            password
        );

        var accessToken = await LoginAsync(
            email,
            password
        );

        var valueLargerThanNumericCapacity =
            10000000000000000.00m;

        var tooLargeResponse =
            await SendCreateProjectRequestAsync(
                accessToken,
                valueLargerThanNumericCapacity
            );

        await AssertBudgetValidationErrorAsync(
            tooLargeResponse
        );

        var valueWithTooManyDecimalPlaces =
            100.999m;

        var excessiveScaleResponse =
            await SendCreateProjectRequestAsync(
                accessToken,
                valueWithTooManyDecimalPlaces
            );

        await AssertBudgetValidationErrorAsync(
            excessiveScaleResponse
        );

        await AssertNoProjectWasCreatedAsync(
            accessToken
        );
    }

    private async Task RegisterClientAsync(
        string email,
        string password)
    {
        var response =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName = "Decimal Test Client",
                    Email = email,
                    Password = password,
                    Role = "Client"
                }
            );

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );
    }

    private async Task<string> LoginAsync(
        string email,
        string password)
    {
        var response =
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
            response.StatusCode
        );

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

        return accessToken!;
    }

    private async Task<HttpResponseMessage>
        SendCreateProjectRequestAsync(
            string accessToken,
            decimal budget)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/projects"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        request.Content = JsonContent.Create(
            new
            {
                Title =
                    $"Invalid Decimal Project {Guid.NewGuid():N}",

                Description =
                    "This project must fail decimal validation.",

                Budget = budget,

                Deadline =
                    DateTimeOffset.UtcNow.AddDays(30)
            }
        );

        return await _client.SendAsync(request);
    }

    private static async Task
        AssertBudgetValidationErrorAsync(
            HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var root = document.RootElement;

        Assert.Equal(
            StatusCodes.Status400BadRequest,
            root.GetProperty("status").GetInt32()
        );

        Assert.True(
            root.TryGetProperty(
                "errors",
                out var errors
            )
        );

        var budgetErrors = errors
            .EnumerateObject()
            .Single(error =>
                string.Equals(
                    error.Name,
                    "Budget",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Value
            .EnumerateArray()
            .Select(error =>
                error.GetString() ?? string.Empty)
            .ToArray();

        Assert.Contains(
            budgetErrors,
            error =>
                error.Contains(
                    "16 integer digits",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                error.Contains(
                    "2 decimal places",
                    StringComparison.OrdinalIgnoreCase
                )
        );
    }

    private async Task AssertNoProjectWasCreatedAsync(
        string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/projects/mine?page=1&pageSize=10"
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var totalItems =
            document.RootElement
                .GetProperty("totalItems")
                .GetInt32();

        Assert.Equal(0, totalItems);
    }
}