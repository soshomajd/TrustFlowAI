using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nethereum.Signer;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class EscrowFlowTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Testpass1";

    private readonly HttpClient _client;

    public EscrowFlowTests(
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
    public async Task ProjectOwner_CanCreateEscrow_ForInProgressProject()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Escrow Test Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Escrow Test Freelancer"
            );

        var clientWalletAddress =
            await ConnectWalletAsync(
                clientAccessToken
            );

        var freelancerWalletAddress =
            await ConnectWalletAsync(
                freelancerAccessToken
            );

        var projectDeadline =
            DateTimeOffset.UtcNow.AddDays(14);

        var projectId =
            await CreateProjectAsync(
                clientAccessToken,
                projectDeadline
            );

        await CreateMilestoneAsync(
            clientAccessToken,
            projectId,
            projectDeadline.AddDays(-5)
        );

        var proposalId =
            await CreateProposalAsync(
                freelancerAccessToken,
                projectId
            );

        await AcceptProposalAsync(
            clientAccessToken,
            projectId,
            proposalId
        );

        using var createEscrowRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow",
                clientAccessToken,
                new
                {
                    ChainId = 11155111,
                    TokenAddress =
                        "0x1111111111111111111111111111111111111111"
                }
            );

        var createEscrowResponse =
            await _client.SendAsync(
                createEscrowRequest
            );

        Assert.Equal(
            HttpStatusCode.Created,
            createEscrowResponse.StatusCode
        );

        await using var responseStream =
            await createEscrowResponse.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var root =
            document.RootElement;

        Assert.Equal(
            projectId,
            root.GetProperty("projectId")
                .GetGuid()
        );

        Assert.Equal(
            11155111,
            root.GetProperty("chainId")
                .GetInt64()
        );

        Assert.Equal(
            "0x1111111111111111111111111111111111111111",
            root.GetProperty("tokenAddress")
                .GetString()
        );

        Assert.Equal(
            clientWalletAddress,
            root.GetProperty("clientWalletAddress")
                .GetString(),
            ignoreCase: true
        );

        Assert.Equal(
            freelancerWalletAddress,
            root.GetProperty("freelancerWalletAddress")
                .GetString(),
            ignoreCase: true
        );

        Assert.Equal(
            10_000m,
            root.GetProperty("totalAmount")
                .GetDecimal()
        );

        Assert.Equal(
            0m,
            root.GetProperty("releasedAmount")
                .GetDecimal()
        );

        Assert.Equal(
            "PendingDeployment",
            root.GetProperty("status")
                .GetString()
        );

        Assert.Equal(
            JsonValueKind.Null,
            root.GetProperty("contractAddress")
                .ValueKind
        );
    }



    [Fact]
    public async Task Deploy_TransitionsPendingDeploymentToAwaitingFunding_AndPersistsContractDetails()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Deploy Test Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Deploy Test Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        await CreateEscrowAsync(
            clientAccessToken,
            projectId
        );

        using var deployRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientAccessToken
            );

        var deployResponse =
            await _client.SendAsync(deployRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            deployResponse.StatusCode
        );

        await using var responseStream =
            await deployResponse.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var root = document.RootElement;

        Assert.Equal(
            "AwaitingFunding",
            root.GetProperty("status").GetString()
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("contractAddress")
                    .GetString()
            )
        );

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("deploymentTransactionHash")
                    .GetString()
            )
        );

        Assert.NotEqual(
            JsonValueKind.Null,
            root.GetProperty("deployedAt").ValueKind
        );
    }

    [Fact]
    public async Task Deploy_ReturnsConflict_WhenEscrowIsNotPendingDeployment()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Deploy Twice Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Deploy Twice Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        await CreateEscrowAsync(
            clientAccessToken,
            projectId
        );

        using var firstDeployRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientAccessToken
            );

        var firstDeployResponse =
            await _client.SendAsync(firstDeployRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            firstDeployResponse.StatusCode
        );

        using var secondDeployRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientAccessToken
            );

        var secondDeployResponse =
            await _client.SendAsync(secondDeployRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            secondDeployResponse.StatusCode
        );
    }

    [Fact]
    public async Task Deploy_ReturnsNotFound_WhenNoEscrowExistsForProject()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Deploy Missing Escrow Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Deploy Missing Escrow Freelancer"
            );

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        using var deployRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientAccessToken
            );

        var deployResponse =
            await _client.SendAsync(deployRequest);

        Assert.Equal(
            HttpStatusCode.NotFound,
            deployResponse.StatusCode
        );
    }

    [Fact]
    public async Task Sync_TransitionsAwaitingFundingToFunded()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Sync Test Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Sync Test Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        await CreateEscrowAsync(
            clientAccessToken,
            projectId
        );

        await DeployEscrowAsync(
            clientAccessToken,
            projectId
        );

        using var syncRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/sync",
                freelancerAccessToken
            );

        var syncResponse =
            await _client.SendAsync(syncRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            syncResponse.StatusCode
        );

        Assert.Equal(
            "Funded",
            await ReadStringPropertyAsync(
                syncResponse,
                "status"
            )
        );
    }

    [Fact]
    public async Task StartMilestone_ReturnsConflict_WhenEscrowIsNotFunded()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Gate Test Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Gate Test Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        await CreateEscrowAsync(
            clientAccessToken,
            projectId
        );

        await DeployEscrowAsync(
            clientAccessToken,
            projectId
        );

        var milestoneId =
            await GetFirstMilestoneIdAsync(
                freelancerAccessToken,
                projectId
            );

        using var startRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}/milestones/{milestoneId}/start",
                freelancerAccessToken
            );

        var startResponse =
            await _client.SendAsync(startRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            startResponse.StatusCode
        );

        var responseBody =
            await startResponse.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "escrow is funded",
            responseBody,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task StartMilestone_Succeeds_AfterEscrowIsFunded()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Gate Success Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Gate Success Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        await CreateEscrowAsync(
            clientAccessToken,
            projectId
        );

        await DeployEscrowAsync(
            clientAccessToken,
            projectId
        );

        using var syncRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/sync",
                clientAccessToken
            );

        await _client.SendAsync(syncRequest);

        var milestoneId =
            await GetFirstMilestoneIdAsync(
                freelancerAccessToken,
                projectId
            );

        using var startRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}/milestones/{milestoneId}/start",
                freelancerAccessToken
            );

        var startResponse =
            await _client.SendAsync(startRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            startResponse.StatusCode
        );

        Assert.Equal(
            "InProgress",
            await ReadStringPropertyAsync(
                startResponse,
                "status"
            )
        );
    }

    [Fact]
    public async Task CreateEscrow_ReturnsConflict_WhenClientWalletIsNotVerified()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Escrow Client Without Wallet"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Escrow Freelancer Without Wallet"
            );

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow",
                clientAccessToken,
                new
                {
                    ChainId = 11155111,
                    TokenAddress =
                        "0x1111111111111111111111111111111111111111"
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode
        );

        var responseBody =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "connect and verify your wallet",
            responseBody,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task CreateEscrow_ReturnsConflict_WhenFreelancerWalletIsNotVerified()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Escrow Client With Wallet"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Escrow Freelancer Without Wallet"
            );

        await ConnectWalletAsync(
            clientAccessToken
        );

        var projectId =
            await CreateInProgressProjectAsync(
                clientAccessToken,
                freelancerAccessToken
            );

        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow",
                clientAccessToken,
                new
                {
                    ChainId = 11155111,
                    TokenAddress =
                        "0x1111111111111111111111111111111111111111"
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode
        );

        var responseBody =
            await response.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "assigned freelancer must connect and verify a wallet",
            responseBody,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private async Task<string>
        RegisterAndLoginAsync(
            string role,
            string fullName)
    {
        var email =
            $"escrow-{role.ToLowerInvariant()}-" +
            $"{Guid.NewGuid():N}@trustflow.test";

        var registerResponse =
            await _client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    FullName = fullName,
                    Email = email,
                    Password,
                    Role = role
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

        return await ReadStringPropertyAsync(
            loginResponse,
            "accessToken"
        );
    }

    private async Task<string>
        ConnectWalletAsync(
            string accessToken)
    {
        var walletKey =
            EthECKey.GenerateKey();

        var walletAddress =
            walletKey.GetPublicAddress();

        using var challengeRequest =
            CreateAuthorizedRequest(
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

        var message =
            await ReadStringPropertyAsync(
                challengeResponse,
                "message"
            );

        var signer =
            new EthereumMessageSigner();

        var signature =
            signer.EncodeUTF8AndSign(
                message,
                walletKey
            );

        using var verifyRequest =
            CreateAuthorizedRequest(
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

        var verifiedWalletAddress =
            await ReadStringPropertyAsync(
                verifyResponse,
                "walletAddress"
            );

        Assert.Equal(
            walletAddress,
            verifiedWalletAddress,
            ignoreCase: true
        );

        return walletAddress;
    }



    private async Task<Guid>
        CreateInProgressProjectAsync(
            string clientAccessToken,
            string freelancerAccessToken)
    {
        var projectDeadline =
            DateTimeOffset.UtcNow.AddDays(14);

        var projectId =
            await CreateProjectAsync(
                clientAccessToken,
                projectDeadline
            );

        await CreateMilestoneAsync(
            clientAccessToken,
            projectId,
            projectDeadline.AddDays(-5)
        );

        var proposalId =
            await CreateProposalAsync(
                freelancerAccessToken,
                projectId
            );

        await AcceptProposalAsync(
            clientAccessToken,
            projectId,
            proposalId
        );

        return projectId;
    }

    private async Task<Guid> CreateEscrowAsync(
        string clientAccessToken,
        Guid projectId)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow",
                clientAccessToken,
                new
                {
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadGuidPropertyAsync(
            response,
            "id"
        );
    }

    private async Task DeployEscrowAsync(
        string clientAccessToken,
        Guid projectId)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientAccessToken
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );
    }

    private async Task<Guid> GetFirstMilestoneIdAsync(
        string accessToken,
        Guid projectId)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}/milestones",
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

        return document.RootElement[0]
            .GetProperty("id")
            .GetGuid();
    }

    private async Task<Guid> CreateProjectAsync(
        string accessToken,
        DateTimeOffset deadline)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                "/api/projects",
                accessToken,
                new
                {
                    Title =
                        "Escrow integration project",

                    Description =
                        "Project created for escrow integration testing.",

                    Budget = 10_000m,

                    Deadline = deadline
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadGuidPropertyAsync(
            response,
            "id"
        );
    }

    private async Task CreateMilestoneAsync(
        string accessToken,
        Guid projectId,
        DateTimeOffset deadline)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/milestones",
                accessToken,
                new
                {
                    Title =
                        "Complete project delivery",

                    Description =
                        "Deliver the complete project.",

                    Amount = 10_000m,

                    SequenceNumber = 1,

                    Deadline = deadline
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );
    }

    private async Task<Guid> CreateProposalAsync(
        string accessToken,
        Guid projectId)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/proposals",
                accessToken,
                new
                {
                    CoverLetter =
                        "I can complete this project.",

                    BidAmount = 10_000m,

                    EstimatedDays = 7
                }
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadGuidPropertyAsync(
            response,
            "id"
        );
    }

    private async Task AcceptProposalAsync(
        string accessToken,
        Guid projectId,
        Guid proposalId)
    {
        using var request =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}/proposals/{proposalId}/accept",
                accessToken
            );

        var response =
            await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );
    }

    private static HttpRequestMessage
        CreateAuthorizedRequest(
            HttpMethod method,
            string url,
            string accessToken,
            object? body = null)
    {
        var request =
            new HttpRequestMessage(
                method,
                url
            );

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken
            );

        if (body is not null)
        {
            request.Content =
                JsonContent.Create(body);
        }

        return request;
    }

    private static async Task<Guid>
        ReadGuidPropertyAsync(
            HttpResponseMessage response,
            string propertyName)
    {
        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        return document.RootElement
            .GetProperty(propertyName)
            .GetGuid();
    }

    private static async Task<string>
        ReadStringPropertyAsync(
            HttpResponseMessage response,
            string propertyName)
    {
        await using var responseStream =
            await response.Content
                .ReadAsStreamAsync();

        using var document =
            await JsonDocument.ParseAsync(
                responseStream
            );

        var value =
            document.RootElement
                .GetProperty(propertyName)
                .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(value)
        );

        return value!;
    }
}