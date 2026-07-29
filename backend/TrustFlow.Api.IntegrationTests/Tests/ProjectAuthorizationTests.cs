using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ProjectAuthorizationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProjectAuthorizationTests(
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
    public async Task Project_CannotBeUpdatedOrDeleted_ByAnotherClient()
    {
        var clientAEmail =
            $"client-a-{Guid.NewGuid():N}@trustflow.test";

        var clientBEmail =
            $"client-b-{Guid.NewGuid():N}@trustflow.test";

        const string password = "Testpass1";

        await RegisterClientAsync(
            clientAEmail,
            password,
            "Client A"
        );

        await RegisterClientAsync(
            clientBEmail,
            password,
            "Client B"
        );

        var clientAToken = await LoginAsync(
            clientAEmail,
            password
        );

        var clientBToken = await LoginAsync(
            clientBEmail,
            password
        );

        var originalTitle =
            $"Owned Project {Guid.NewGuid():N}";

        var projectId = await CreateProjectAsync(
            clientAToken,
            originalTitle
        );

        using var updateRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Put,
                $"/api/projects/{projectId}",
                clientBToken,
                new
                {
                    Title = "Unauthorized Update",

                    Description =
                        "Client B must not be able to update this project.",

                    Budget = 2500.00m,

                    Deadline =
                        DateTimeOffset.UtcNow.AddDays(45)
                }
            );

        var updateResponse =
            await _client.SendAsync(updateRequest);

        Assert.Equal(
            HttpStatusCode.NotFound,
            updateResponse.StatusCode
        );

        using var deleteRequest =
            CreateAuthorizedRequest(
                HttpMethod.Delete,
                $"/api/projects/{projectId}",
                clientBToken
            );

        var deleteResponse =
            await _client.SendAsync(deleteRequest);

        Assert.Equal(
            HttpStatusCode.NotFound,
            deleteResponse.StatusCode
        );

        var projectResponse =
            await _client.GetAsync(
                $"/api/projects/{projectId}"
            );

        Assert.Equal(
            HttpStatusCode.OK,
            projectResponse.StatusCode
        );

        await using var projectStream =
            await projectResponse.Content
                .ReadAsStreamAsync();

        using var projectDocument =
            await JsonDocument.ParseAsync(
                projectStream
            );

        var returnedProjectId =
            projectDocument.RootElement
                .GetProperty("id")
                .GetGuid();

        var returnedTitle =
            projectDocument.RootElement
                .GetProperty("title")
                .GetString();

        Assert.Equal(
            projectId,
            returnedProjectId
        );

        Assert.Equal(
            originalTitle,
            returnedTitle
        );
    }

    private async Task RegisterClientAsync(
        string email,
        string password,
        string fullName)
    {
        var response =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName = fullName,
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

    private async Task<Guid> CreateProjectAsync(
        string accessToken,
        string title)
    {
        using var request =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/projects",
                accessToken,
                new
                {
                    Title = title,

                    Description =
                        "Project created by Client A.",

                    Budget = 2000.00m,

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