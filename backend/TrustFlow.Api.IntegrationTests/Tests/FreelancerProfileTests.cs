using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Freelancers;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class FreelancerProfileTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Profile_SaveReadUpdateAndClear_PersistsForClientAndOwner()
    {
        using var freelancer = await CreateAccountAsync("Freelancer");
        using var client = await CreateAccountAsync("Client");
        var initial = await ReadProfileAsync(freelancer.Client, "me");
        Assert.Equal(freelancer.Id, initial.UserId);
        Assert.Empty(initial.Skills);
        Assert.Null(initial.UpdatedAt);

        var input = new UpdateFreelancerProfileRequest
        {
            Headline = "  Frontend developer  ",
            About = "I build accessible websites.",
            Location = "Tehran",
            YearsOfExperience = 3,
            Availability = "Available",
            Skills = [" React ", "react", "TypeScript"],
            Languages = [" Persian ", "English"],
            PortfolioItems = [new() { Title = " Booking app ", Description = "Built the booking flow.", Url = "https://example.com/work" }]
        };
        var save = await freelancer.Client.PutAsJsonAsync("/api/freelancers/me/profile", input);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var viewed = await ReadProfileAsync(client.Client, freelancer.Id.ToString());
        Assert.Equal("Frontend developer", viewed.Headline);
        Assert.Equal(input.About, viewed.About);
        Assert.Equal(3, viewed.YearsOfExperience);
        Assert.Equal("Available", viewed.Availability);
        Assert.Equal(["React", "TypeScript"], viewed.Skills);
        Assert.Equal(["Persian", "English"], viewed.Languages);
        Assert.Equal("Booking app", Assert.Single(viewed.PortfolioItems).Title);
        Assert.NotNull(viewed.UpdatedAt);

        input.PortfolioItems = [new() { Title = "Replacement sample", Url = "https://example.com/new" }];
        Assert.Equal(HttpStatusCode.OK, (await freelancer.Client.PutAsJsonAsync("/api/freelancers/me/profile", input)).StatusCode);
        Assert.Equal("Replacement sample", Assert.Single((await ReadProfileAsync(client.Client, freelancer.Id.ToString())).PortfolioItems).Title);

        Assert.Equal(HttpStatusCode.OK, (await freelancer.Client.PutAsJsonAsync("/api/freelancers/me/profile", new UpdateFreelancerProfileRequest())).StatusCode);
        var cleared = await ReadProfileAsync(client.Client, freelancer.Id.ToString());
        Assert.Empty(cleared.Headline);
        Assert.Empty(cleared.Skills);
        Assert.Empty(cleared.Languages);
        Assert.Empty(cleared.PortfolioItems);
        Assert.Null(cleared.YearsOfExperience);
    }

    [Fact]
    public async Task Profile_RequiresAuthenticationAndFreelancerWriteRole()
    {
        using var freelancer = await CreateAccountAsync("Freelancer");
        using var client = await CreateAccountAsync("Client");
        using var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/freelancers/{freelancer.Id}/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync("/api/freelancers/me/profile", new { Headline = "No access" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Client.GetAsync("/api/freelancers/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.Client.PutAsJsonAsync("/api/freelancers/me/profile", new { Headline = "No access" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await freelancer.Client.GetAsync($"/api/freelancers/{client.Id}/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.Client.GetAsync($"/api/freelancers/{Guid.NewGuid()}/profile")).StatusCode);
    }

    [Fact]
    public async Task Profile_EditingIsBoundToCaller_AndTrustFieldsCannotBeWritten()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        using var other = await CreateAccountAsync("Freelancer");
        var response = await other.Client.PutAsJsonAsync("/api/freelancers/me/profile", new
        {
            UserId = owner.Id,
            Headline = "Other freelancer",
            FullName = "Pretend name",
            WalletVerified = true,
            CompletedProjectCount = 999,
            CompletedProjects = new[] { new { Title = "Pretend project" } }
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadProfileAsync(owner.Client, "me")).Headline);
        var otherProfile = await ReadProfileAsync(other.Client, "me");
        Assert.Equal(other.Id, otherProfile.UserId);
        Assert.Equal("Other freelancer", otherProfile.Headline);
        Assert.NotEqual("Pretend name", otherProfile.FullName);
        Assert.False(otherProfile.WalletVerified);
        Assert.Equal(0, otherProfile.CompletedProjectCount);
        Assert.Empty(otherProfile.CompletedProjects);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await other.Client.PutAsJsonAsync($"/api/freelancers/{owner.Id}/profile", new { Headline = "No access" })).StatusCode);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,hello")]
    [InlineData("https://user:password@example.com")]
    [InlineData("not-a-link")]
    public async Task Profile_RejectsUnsafePortfolioLinks(string url)
    {
        using var owner = await CreateAccountAsync("Freelancer");
        var response = await owner.Client.PutAsJsonAsync("/api/freelancers/me/profile", new
        {
            PortfolioItems = new[] { new { Title = "Sample", Url = url } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await ReadProfileAsync(owner.Client, "me")).UpdatedAt);
    }

    [Fact]
    public async Task Profile_RejectsInvalidNestedDataAndLimits_WithoutSavingPartialChanges()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        object[] invalidInputs =
        [
            new { Headline = new string('x', 101) },
            new { Headline = (string?)null },
            new { About = new string('x', 1201) },
            new { YearsOfExperience = 61 },
            new { Availability = "Anything" },
            new { Skills = Enumerable.Range(0, 11).Select(i => $"Skill {i}").ToArray() },
            new { Skills = new[] { " " } },
            new { Skills = new[] { new string('x', 41) } },
            new { Skills = (string[]?)null },
            new { Languages = new[] { "a", "b", "c", "d", "e", "f" } },
            new { PortfolioItems = new object?[] { null } },
            new { PortfolioItems = Enumerable.Range(0, 4).Select(i => new { Title = $"Item {i}" }).ToArray() },
            new { PortfolioItems = new[] { new { Title = " " } } },
            new { PortfolioItems = new[] { new { Title = "Sample", Description = new string('x', 501) } } }
        ];
        foreach (var input in invalidInputs)
        {
            var response = await owner.Client.PutAsJsonAsync("/api/freelancers/me/profile", input);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
                $"Expected 400 for {JsonSerializer.Serialize(input)}; received {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        Assert.Null((await ReadProfileAsync(owner.Client, "me")).UpdatedAt);
    }

    [Fact]
    public async Task Profile_HistoryUsesOnlyThisFreelancersCompletedProjects_AndOmitsPrivateData()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        using var other = await CreateAccountAsync("Freelancer");
        using var client = await CreateAccountAsync("Client");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var completed = new Project
            {
                ClientId = client.Id, Title = "Completed website",
                Description = "Private project details", Budget = 500m,
                Deadline = DateTimeOffset.UtcNow.AddDays(10), Status = ProjectStatus.Completed,
                // Work is assigned per milestone now, so the history is built
                // from the stages a freelancer holds, not from the project row.
                Milestones = [new MileStone { Title = "Delivery", Description = "Private delivery", Amount = 500m, SequenceNumber = 1, Deadline = DateTimeOffset.UtcNow.AddDays(5), Status = MileStoneStatus.Approved, FreelancerId = owner.Id }]
            };
            db.Projects.AddRange(completed,
                new Project { ClientId = client.Id, Title = "Still in progress", Budget = 100m, Status = ProjectStatus.InProgress, Deadline = DateTimeOffset.UtcNow.AddDays(10),
                    Milestones = [new MileStone { Title = "In flight", Description = "Ongoing stage", Amount = 100m, SequenceNumber = 1, Deadline = DateTimeOffset.UtcNow.AddDays(5), Status = MileStoneStatus.InProgress, FreelancerId = owner.Id }] },
                new Project { ClientId = client.Id, Title = "Someone else's project", Budget = 100m, Status = ProjectStatus.Completed, Deadline = DateTimeOffset.UtcNow.AddDays(10),
                    Milestones = [new MileStone { Title = "Their delivery", Description = "Their stage", Amount = 100m, SequenceNumber = 1, Deadline = DateTimeOffset.UtcNow.AddDays(5), Status = MileStoneStatus.Approved, FreelancerId = other.Id }] });
            await db.SaveChangesAsync();
        }
        var response = await client.Client.GetAsync($"/api/freelancers/{owner.Id}/profile");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<FreelancerProfileResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(1, profile.CompletedProjectCount);
        var project = Assert.Single(profile.CompletedProjects);
        Assert.Equal("Completed website", project.Title);
        Assert.Equal(1, project.MilestoneCount);
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.TryGetProperty("email", out _));
        Assert.False(json.RootElement.TryGetProperty("walletAddress", out _));
        Assert.DoesNotContain("Private project details", body);
        Assert.DoesNotContain("Private delivery", body);
        Assert.DoesNotContain("budget", body);
        Assert.DoesNotContain("clientId", body);
        Assert.DoesNotContain("Someone else's project", body);
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });

    private async Task<Account> CreateAccountAsync(string role)
    {
        var client = NewClient();
        var email = $"profile-{Guid.NewGuid():N}@trustflow.test";
        const string password = "Testpass1";
        var register = await client.PostAsJsonAsync("/api/auth/register", new { FullName = $"Profile {role}", Email = email, Password = password, Role = role });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await TestAuth.ConfirmEmailAsync(client, email);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return new Account(registered.GetProperty("id").GetGuid(), client);
    }

    private static async Task<FreelancerProfileResponse> ReadProfileAsync(HttpClient client, string id)
    {
        var response = await client.GetAsync($"/api/freelancers/{id}/profile");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FreelancerProfileResponse>())!;
    }

    private sealed record Account(Guid Id, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
