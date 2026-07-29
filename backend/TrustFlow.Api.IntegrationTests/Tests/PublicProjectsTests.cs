using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class PublicProjectsTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PublicProjectsTests(
        CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress =
                    new Uri("https://localhost"),

                AllowAutoRedirect = false
            }
        );
    }

    [Fact]
    public async Task GetProjects_WithoutAuthentication_ReturnsOk()
    {
        var response = await _client.GetAsync(
            "/api/projects?page=1&pageSize=10"
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

        var root = document.RootElement;

        Assert.True(
            root.TryGetProperty(
                "items",
                out var items
            )
        );

        Assert.Equal(
            JsonValueKind.Array,
            items.ValueKind
        );

        Assert.True(
            root.TryGetProperty(
                "totalItems",
                out _
            )
        );

        Assert.True(
            root.TryGetProperty(
                "totalPages",
                out _
            )
        );
    }
}