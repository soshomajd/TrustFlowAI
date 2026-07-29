using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class MilestoneWorkflowTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MilestoneWorkflowTests(
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
    public async Task Milestones_MustRunSequentially_AndCompleteProject()
    {
        var uniqueValue =
            Guid.NewGuid().ToString("N");

        var clientEmail =
            $"milestone-client-{uniqueValue}@trustflow.test";

        var freelancerEmail =
            $"milestone-freelancer-{uniqueValue}@trustflow.test";

        const string password = "Testpass1";

        await RegisterAsync(
            clientEmail,
            password,
            "Milestone Client",
            "Client"
        );

        await RegisterAsync(
            freelancerEmail,
            password,
            "Milestone Freelancer",
            "Freelancer"
        );

        var clientToken = await LoginAsync(
            clientEmail,
            password
        );

        var freelancerToken = await LoginAsync(
            freelancerEmail,
            password
        );

        var projectDeadline =
            DateTimeOffset.UtcNow.AddDays(90);

        var projectId = await CreateProjectAsync(
            clientToken,
            projectDeadline
        );

        var milestoneOneId =
            await CreateMilestoneAsync(
                projectId,
                clientToken,
                title: "Backend implementation",
                amount: 1000.00m,
                deadline:
                    DateTimeOffset.UtcNow.AddDays(30),
                sequenceNumber: 1
            );

        var milestoneTwoId =
            await CreateMilestoneAsync(
                projectId,
                clientToken,
                title: "Frontend implementation",
                amount: 1500.00m,
                deadline:
                    DateTimeOffset.UtcNow.AddDays(60),
                sequenceNumber: 2
            );

        var proposalId =
            await CreateProposalAsync(
                projectId,
                freelancerToken
            );

        await AcceptProposalAsync(
            projectId,
            proposalId,
            clientToken
        );

        using var earlyStartRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}" +
                $"/milestones/{milestoneTwoId}/start",
                freelancerToken
            );

        var earlyStartResponse =
            await _client.SendAsync(
                earlyStartRequest
            );

        Assert.Equal(
            HttpStatusCode.Conflict,
            earlyStartResponse.StatusCode
        );

        var earlyStartBody =
            await earlyStartResponse.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "Previous milestones must be approved",
            earlyStartBody,
            StringComparison.OrdinalIgnoreCase
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            "start",
            freelancerToken,
            HttpStatusCode.OK
        );

        await AssertMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            clientToken,
            "InProgress"
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            "submit",
            freelancerToken,
            HttpStatusCode.OK
        );

        await AssertMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            clientToken,
            "Submitted"
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            "approve",
            clientToken,
            HttpStatusCode.OK
        );

        await AssertMilestoneStatusAsync(
            projectId,
            milestoneOneId,
            clientToken,
            "Approved"
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneTwoId,
            "start",
            freelancerToken,
            HttpStatusCode.OK
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneTwoId,
            "submit",
            freelancerToken,
            HttpStatusCode.OK
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            milestoneTwoId,
            "approve",
            clientToken,
            HttpStatusCode.OK
        );

        await AssertMilestoneStatusAsync(
            projectId,
            milestoneTwoId,
            clientToken,
            "Approved"
        );

        await AssertProjectCompletedAsync(
            projectId,
            clientToken
        );

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
        string clientToken,
        DateTimeOffset deadline)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/projects",
                clientToken,
                new
                {
                    Title =
                        $"Milestone Test Project {Guid.NewGuid():N}",

                    Description =
                        "Integration test for sequential milestones.",

                    Budget = 2500.00m,

                    Deadline = deadline
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadIdAsync(response);
    }

    private async Task<Guid> CreateMilestoneAsync(
        Guid projectId,
        string clientToken,
        string title,
        decimal amount,
        DateTimeOffset deadline,
        int sequenceNumber)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/milestones",
                clientToken,
                new
                {
                    Title = title,

                    Description =
                        $"Description for {title}.",

                    Amount = amount,

                    Deadline = deadline,

                    SequenceNumber = sequenceNumber
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadIdAsync(response);
    }

    private async Task<Guid> CreateProposalAsync(
        Guid projectId,
        string freelancerToken)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/proposals",
                freelancerToken,
                new
                {
                    CoverLetter =
                        "I can complete both milestones.",

                    BidAmount = 2400.00m,

                    EstimatedDays = 55
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadIdAsync(response);
    }

    private async Task AcceptProposalAsync(
        Guid projectId,
        Guid proposalId,
        string clientToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}" +
                $"/proposals/{proposalId}/accept",
                clientToken
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );
    }

    private async Task ChangeMilestoneStatusAsync(
        Guid projectId,
        Guid milestoneId,
        string action,
        string accessToken,
        HttpStatusCode expectedStatusCode)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}" +
                $"/milestones/{milestoneId}/{action}",
                accessToken
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            expectedStatusCode,
            response.StatusCode
        );
    }

    private async Task AssertMilestoneStatusAsync(
        Guid projectId,
        Guid milestoneId,
        string accessToken,
        string expectedStatus)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}" +
                $"/milestones/{milestoneId}",
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

        var actualStatus =
            document.RootElement
                .GetProperty("status")
                .GetString();

        Assert.Equal(
            expectedStatus,
            actualStatus
        );
    }

    private async Task AssertProjectCompletedAsync(
        Guid projectId,
        string clientToken)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}/workspace",
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

        var root = document.RootElement;

        var projectStatus =
            root.GetProperty("status")
                .GetString();

        Assert.Equal(
            "Completed",
            projectStatus
        );

        var milestones =
            root.GetProperty("milestones")
                .EnumerateArray()
                .ToList();

        Assert.Equal(
            2,
            milestones.Count
        );

        Assert.All(
            milestones,
            milestone =>
            {
                var status =
                    milestone.GetProperty("status")
                        .GetString();

                Assert.Equal(
                    "Approved",
                    status
                );
            }
        );
    }

    private static async Task<Guid> ReadIdAsync(
        HttpResponseMessage response)
    {
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