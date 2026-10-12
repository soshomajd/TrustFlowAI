using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ProposalAcceptanceTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public ProposalAcceptanceTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;

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
    public async Task AcceptProposal_AssignsFreelancer_AndRejectsOtherProposals()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");

        var clientEmail =
            $"proposal-client-{uniqueValue}@trustflow.test";

        var freelancerAEmail =
            $"proposal-freelancer-a-{uniqueValue}@trustflow.test";

        var freelancerBEmail =
            $"proposal-freelancer-b-{uniqueValue}@trustflow.test";

        const string password = "Testpass1";

        await RegisterAsync(
            clientEmail,
            password,
            "Proposal Client",
            "Client"
        );

        await RegisterAsync(
            freelancerAEmail,
            password,
            "Freelancer A",
            "Freelancer"
        );

        await RegisterAsync(
            freelancerBEmail,
            password,
            "Freelancer B",
            "Freelancer"
        );

        var clientToken = await LoginAsync(
            clientEmail,
            password
        );

        var freelancerAToken = await LoginAsync(
            freelancerAEmail,
            password
        );

        var freelancerBToken = await LoginAsync(
            freelancerBEmail,
            password
        );

        var projectId = await CreateProjectAsync(
            clientToken
        );
        var milestoneId = await CreateMilestoneAsync(
    projectId,
    clientToken
);

        // Both freelancers apply for the same single stage, so accepting one
        // has to reject the other as overlapping.
        var proposalAId = await CreateProposalAsync(
            projectId,
            freelancerAToken,
            "Proposal from Freelancer A",
            [milestoneId],
            20
        );

        var proposalBId = await CreateProposalAsync(
            projectId,
            freelancerBToken,
            "Proposal from Freelancer B",
            [milestoneId],
            25
        );

        using var acceptRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}" +
                $"/proposals/{proposalAId}/accept",
                clientToken
            );

        var acceptResponse =
            await _client.SendAsync(acceptRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            acceptResponse.StatusCode
        );

        var proposalStatuses =
            await GetProposalStatusesAsync(
                projectId,
                clientToken
            );

        Assert.True(
            proposalStatuses.TryGetValue(
                proposalAId,
                out var proposalAStatus
            )
        );

        Assert.Equal(
            "Accepted",
            proposalAStatus
        );

        Assert.True(
            proposalStatuses.TryGetValue(
                proposalBId,
                out var proposalBStatus
            )
        );

        Assert.Equal(
            "Rejected",
            proposalBStatus
        );

        var assignedProjectStatus =
            await GetAssignedProjectStatusAsync(
                projectId,
                freelancerAToken
            );

        Assert.Equal(
            "InProgress",
            assignedProjectStatus
        );

        var freelancerBProjectStatus =
            await GetAssignedProjectStatusAsync(
                projectId,
                freelancerBToken
            );

        Assert.Null(freelancerBProjectStatus);

        var publicProjectResponse =
            await _client.GetAsync(
                $"/api/projects/{projectId}"
            );

        Assert.Equal(
            HttpStatusCode.NotFound,
            publicProjectResponse.StatusCode
        );
    }

    // The API blocks milestone edits while a proposal is live, so this is the
    // last line of defence: even if an amount does change underneath a
    // pending proposal, the freelancer can only be bound to the price they
    // actually applied at.
    [Fact]
    public async Task AcceptProposal_RejectsPriceThatChangedAfterTheFreelancerApplied()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");
        const string password = "Testpass1";
        var clientEmail = $"mismatch-client-{uniqueValue}@trustflow.test";
        var freelancerEmail = $"mismatch-freelancer-{uniqueValue}@trustflow.test";

        await RegisterAsync(clientEmail, password, "Mismatch Client", "Client");
        await RegisterAsync(freelancerEmail, password, "Mismatch Freelancer", "Freelancer");

        var clientToken = await LoginAsync(clientEmail, password);
        var freelancerToken = await LoginAsync(freelancerEmail, password);
        var projectId = await CreateProjectAsync(clientToken);
        var milestoneId = await CreateMilestoneAsync(projectId, clientToken);
        var proposalId = await CreateProposalAsync(
            projectId,
            freelancerToken,
            "I will deliver this stage for the amount shown.",
            [milestoneId],
            20
        );

        // Move 200 of the agreed price onto a new stage. The project still
        // allocates its whole budget, so only the claimed stage's price has
        // moved out from under the pending proposal.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var milestone = await database.Milestones
                .SingleAsync(item => item.Id == milestoneId);

            milestone.Amount = 1800.00m;

            database.Milestones.Add(new MileStone
            {
                ProjectId = projectId,
                Title = "Added after the proposal",
                Description = "A stage the freelancer never applied for.",
                Amount = 200.00m,
                SequenceNumber = 2,
                Deadline = DateTimeOffset.UtcNow.AddDays(45)
            });

            await database.SaveChangesAsync();
        }

        using var acceptRequest = CreateAuthorizedRequest(
            HttpMethod.Patch,
            $"/api/projects/{projectId}/proposals/{proposalId}/accept",
            clientToken
        );

        var response = await _client.SendAsync(acceptRequest);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1800.00m, problem.GetProperty("currentTotal").GetDecimal());
        Assert.Equal(2000.00m, problem.GetProperty("agreedPrice").GetDecimal());

        var statuses = await GetProposalStatusesAsync(projectId, clientToken);
        Assert.Equal("Pending", statuses[proposalId]);
        Assert.Null(await GetAssignedProjectStatusAsync(projectId, freelancerToken));
    }

    private async Task RegisterAsync(
        string email,
        string password,
        string fullName,
        string role)
    {
        var response =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName = fullName,
                    Email = email,
                    Password = password,
                    Role = role
                }
            );

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );
        await TestAuth.ConfirmEmailAsync(_client, email);
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

    private async Task<Guid> CreateProjectAsync(
        string clientToken)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/projects",
                clientToken,
                new
                {
                    Title =
                        $"Proposal Test Project {Guid.NewGuid():N}",

                    Description =
                        "Integration test for proposal acceptance.",

                    Budget = 2000.00m,

                    Deadline =
                        DateTimeOffset.UtcNow.AddDays(60)
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        return document.RootElement
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> CreateProposalAsync(
        Guid projectId,
        string freelancerToken,
        string coverLetter,
        Guid[] milestoneIds,
        int estimatedDays)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/proposals",
                freelancerToken,
                new
                {
                    CoverLetter = coverLetter,

                    // The price is the sum of the milestones claimed, so
                    // the proposal names them instead of naming a bid.
                    MilestoneIds = milestoneIds,

                    EstimatedDays = estimatedDays
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        return document.RootElement
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Dictionary<Guid, string>>
        GetProposalStatusesAsync(
            Guid projectId,
            string clientToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}" +
                "/proposals?page=1&pageSize=20",
                clientToken
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

        var statuses = new Dictionary<Guid, string>();

        foreach (var item in document.RootElement
                     .GetProperty("items")
                     .EnumerateArray())
        {
            var proposalId =
                item.GetProperty("id").GetGuid();

            var status =
                item.GetProperty("status").GetString()
                ?? string.Empty;

            statuses[proposalId] = status;
        }

        return statuses;
    }

    private async Task<string?>
        GetAssignedProjectStatusAsync(
            Guid projectId,
            string freelancerToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                "/api/projects/assigned-to-me" +
                "?page=1&pageSize=100",
                freelancerToken
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

        foreach (var item in document.RootElement
                     .GetProperty("items")
                     .EnumerateArray())
        {
            if (item.GetProperty("id").GetGuid() !=
                projectId)
            {
                continue;
            }

            return item
                .GetProperty("status")
                .GetString();
        }

        return null;
    }

    private static HttpRequestMessage
        CreateAuthorizedJsonRequest(
            HttpMethod method,
            string requestUri,
            string accessToken,
            object body)
    {
        var request =
            CreateAuthorizedRequest(
                method,
                requestUri,
                accessToken
            );

        request.Content = JsonContent.Create(body);

        return request;
    }

    private static HttpRequestMessage
        CreateAuthorizedRequest(
            HttpMethod method,
            string requestUri,
            string accessToken)
    {
        var request = new HttpRequestMessage(
            method,
            requestUri
        );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        return request;
    }
    private async Task<Guid> CreateMilestoneAsync(
    Guid projectId,
    string clientToken)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/milestones",
                clientToken,
                new
                {
                    Title =
                        "Proposal acceptance milestone",

                    Description =
                        "Complete project delivery for proposal testing.",

                    Amount = 2000.00m,

                    SequenceNumber = 1,

                    Deadline =
                        DateTimeOffset.UtcNow.AddDays(30)
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        return document.RootElement
            .GetProperty("id")
            .GetGuid();
    }
}
