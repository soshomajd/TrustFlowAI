using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ProposalAcceptanceTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProposalAcceptanceTests(
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

        var proposalAId = await CreateProposalAsync(
            projectId,
            freelancerAToken,
            "Proposal from Freelancer A",
            1800.00m,
            20
        );

        var proposalBId = await CreateProposalAsync(
            projectId,
            freelancerBToken,
            "Proposal from Freelancer B",
            1750.00m,
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
        decimal bidAmount,
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
                    BidAmount = bidAmount,
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
}