using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Models.Enums;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class PublicProjectsTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public PublicProjectsTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress =
                    new Uri("https://localhost"),

                AllowAutoRedirect = false
            }
        );
    }

    [Theory]
    [InlineData(false, 100)]
    [InlineData(true, 70)]
    public async Task MarketplaceAvailableAmount_IsTheSumOfUnassignedStages(bool partlyAssigned, decimal expected)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var project = new Project
        {
            Title = "AvailableBudget" + suffix, Description = "Marketplace amount regression",
            Budget = 100m, Status = partlyAssigned ? ProjectStatus.InProgress : ProjectStatus.Open,
            Deadline = DateTimeOffset.UtcNow.AddDays(14)
        };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = new ApplicationUser { FullName = "Budget Client", UserName = "client" + suffix };
            var freelancer = new ApplicationUser { FullName = "Budget Freelancer", UserName = "freelancer" + suffix };
            db.Users.AddRange(client, freelancer);
            project.ClientId = client.Id;
            project.Milestones.Add(new MileStone
            {
                Title = "First", Amount = 30m, SequenceNumber = 1,
                Deadline = project.Deadline.AddDays(-5), FreelancerId = partlyAssigned ? freelancer.Id : null
            });
            project.Milestones.Add(new MileStone
            {
                Title = "Second", Amount = 70m, SequenceNumber = 2,
                Deadline = project.Deadline.AddDays(-1)
            });
            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }
        var list = await _client.GetAsync($"/api/projects?search={project.Title}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var listPayload = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var row = Assert.Single(listPayload.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(expected, row.GetProperty("availableAmount").GetDecimal());
        Assert.Equal(100m, row.GetProperty("allocatedAmount").GetDecimal());
        var details = await _client.GetAsync($"/api/projects/{project.Id}");
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        using var detailsPayload = JsonDocument.Parse(await details.Content.ReadAsStringAsync());
        Assert.Equal(expected, detailsPayload.RootElement.GetProperty("availableAmount").GetDecimal());
        Assert.Equal(100m, detailsPayload.RootElement.GetProperty("budget").GetDecimal());
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