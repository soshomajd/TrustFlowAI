using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Signer;
using TrustFlow.Api.Data;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Enums;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class EscrowFlowTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "Testpass1";

    private readonly HttpClient _client;

    private readonly CustomWebApplicationFactory _factory;

    public EscrowFlowTests(
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

        var milestoneId = await CreateMilestoneAsync(
            clientAccessToken,
            projectId,
            projectDeadline.AddDays(-5)
        );

        var proposalId =
            await CreateProposalAsync(
                freelancerAccessToken,
                projectId,
                milestoneId
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
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
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
            31337,
            root.GetProperty("chainId")
                .GetInt64()
        );

        Assert.Equal(
            "0x5fbdb2315678afecb367f032d93f642f64180aa3",
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
    public async Task CreateEscrow_RejectsChainAndTokenThatDifferFromConfiguration()
    {
        var clientAccessToken = await RegisterAndLoginAsync(
            role: "Client",
            fullName: "Configured Chain Client"
        );
        var freelancerAccessToken = await RegisterAndLoginAsync(
            role: "Freelancer",
            fullName: "Configured Chain Freelancer"
        );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId = await CreateInProgressProjectAsync(
            clientAccessToken,
            freelancerAccessToken
        );

        using (var wrongChainRequest = CreateAuthorizedRequest(
                   HttpMethod.Post,
                   $"/api/projects/{projectId}/escrow",
                   clientAccessToken,
                   new
                   {
                       ChainId = 11_155_111,
                       TokenAddress =
                           "0x5FbDB2315678afecb367f032d93F642f64180aa3"
                   }))
        {
            var response = await _client.SendAsync(wrongChainRequest);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(
                "configured chain",
                await response.Content.ReadAsStringAsync(),
                StringComparison.OrdinalIgnoreCase
            );
        }

        using var wrongTokenRequest = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/escrow",
            clientAccessToken,
            new
            {
                ChainId = 31_337,
                TokenAddress =
                    "0x0000000000000000000000000000000000000001"
            }
        );
        var wrongTokenResponse =
            await _client.SendAsync(wrongTokenRequest);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            wrongTokenResponse.StatusCode
        );
        Assert.Contains(
            "configured payment token",
            await wrongTokenResponse.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase
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

        var syncResponse = await _client.SendAsync(syncRequest);
        var contractAddress = await ReadStringPropertyAsync(
            syncResponse,
            "contractAddress"
        );
        FakeEscrowChainDeployer.MarkWorkStarted(contractAddress);

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
    public async Task ApproveMilestone_RequiresOnChainRelease_ThenCompletesEscrow()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Release Gate Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Release Gate Freelancer"
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

        var syncResponse =
            await _client.SendAsync(syncRequest);

        var contractAddress =
            await ReadStringPropertyAsync(
                syncResponse,
                "contractAddress"
            );

        FakeEscrowChainDeployer.MarkWorkStarted(
            contractAddress
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

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.SendAsync(startRequest))
                .StatusCode
        );

        using var submitRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/milestones/{milestoneId}/submissions",
                freelancerAccessToken
            );
        submitRequest.Content = new MultipartFormDataContent
        {
            { new StringContent("Completed work for client review."), "note" },
            { new StringContent("https://example.test/delivery"), "previewLink" }
        };

        Assert.Equal(
            HttpStatusCode.OK,
            (await _client.SendAsync(submitRequest))
                .StatusCode
        );

        using var earlyApproveRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}/milestones/{milestoneId}/approve",
                clientAccessToken
            );

        var earlyApproveResponse =
            await _client.SendAsync(earlyApproveRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            earlyApproveResponse.StatusCode
        );

        Assert.Contains(
            "has not released this milestone on-chain",
            await earlyApproveResponse.Content
                .ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase
        );

        FakeEscrowChainDeployer.MarkMilestoneReleased(
            contractAddress,
            0
        );

        using var approveRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}/milestones/{milestoneId}/approve",
                clientAccessToken
            );

        var approveResponse =
            await _client.SendAsync(approveRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            approveResponse.StatusCode
        );

        await using var approveStream =
            await approveResponse.Content
                .ReadAsStreamAsync();

        using var approveDocument =
            await JsonDocument.ParseAsync(
                approveStream
            );

        Assert.Equal(
            "Approved",
            approveDocument.RootElement
                .GetProperty("status")
                .GetString()
        );

        Assert.Equal(
            "Completed",
            approveDocument.RootElement
                .GetProperty("projectStatus")
                .GetString()
        );

        using var escrowGetRequest =
            CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}/escrow",
                clientAccessToken
            );

        var escrowGetResponse =
            await _client.SendAsync(escrowGetRequest);

        await using var escrowStream =
            await escrowGetResponse.Content
                .ReadAsStreamAsync();

        using var escrowDocument =
            await JsonDocument.ParseAsync(
                escrowStream
            );

        Assert.Equal(
            "Completed",
            escrowDocument.RootElement
                .GetProperty("status")
                .GetString()
        );

        Assert.Equal(
            10_000m,
            escrowDocument.RootElement
                .GetProperty("releasedAmount")
                .GetDecimal()
        );

        Assert.False(
            string.IsNullOrEmpty(
                escrowDocument.RootElement
                    .GetProperty("completedAt")
                    .GetString()
            )
        );
    }

    [Fact]
    public async Task Sync_ReconcilesOnChainCancellation()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Cancellation Sync Client"
            );

        var freelancerAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Cancellation Sync Freelancer"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(freelancerAccessToken);

        var projectId = await CreateInProgressProjectAsync(
            clientAccessToken,
            freelancerAccessToken
        );

        await CreateEscrowAsync(clientAccessToken, projectId);
        await DeployEscrowAsync(clientAccessToken, projectId);

        using (var fundingSyncRequest = CreateAuthorizedRequest(
                   HttpMethod.Post,
                   $"/api/projects/{projectId}/escrow/sync",
                   clientAccessToken))
        {
            var fundingSyncResponse =
                await _client.SendAsync(fundingSyncRequest);

            Assert.Equal(
                "Funded",
                await ReadStringPropertyAsync(
                    fundingSyncResponse,
                    "status"
                )
            );
        }

        using var escrowRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/escrow",
            clientAccessToken
        );
        var escrowResponse = await _client.SendAsync(escrowRequest);
        var contractAddress = await ReadStringPropertyAsync(
            escrowResponse,
            "contractAddress"
        );

        FakeEscrowChainDeployer.MarkCancelled(contractAddress);

        using var cancellationSyncRequest = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/escrow/sync",
            clientAccessToken
        );
        var cancellationSyncResponse =
            await _client.SendAsync(cancellationSyncRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            cancellationSyncResponse.StatusCode
        );
        Assert.Equal(
            "Cancelled",
            await ReadStringPropertyAsync(
                cancellationSyncResponse,
                "status"
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteProject_PreservesCancelledEscrowHistory(bool wasFunded)
    {
        var client = await RegisterAndLoginAsync("Client", "History owner");
        var freelancer = await RegisterAndLoginAsync("Freelancer", "History freelancer");
        await ConnectWalletAsync(client);
        await ConnectWalletAsync(freelancer);
        var projectId = await CreateInProgressProjectAsync(client, freelancer);
        var escrowId = await CreateEscrowAsync(client, projectId);
        await DeployEscrowAsync(client, projectId);
        using var read = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/escrow", client);
        using var before = await _client.SendAsync(read);
        var address = await ReadStringPropertyAsync(before, "contractAddress");
        if (wasFunded)
        {
            using var fund = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/escrow/sync", client);
            using var funded = await _client.SendAsync(fund);
            Assert.Equal(HttpStatusCode.OK, funded.StatusCode);
        }
        FakeEscrowChainDeployer.MarkCancelled(address, wasFunded);
        using var sync = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{projectId}/escrow/sync", client);
        using var cancelled = await _client.SendAsync(sync);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        using var delete = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/projects/{projectId}", client);
        using var deleted = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Contains("escrow history", await deleted.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        using var history = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{projectId}/escrow", freelancer);
        using var historyResponse = await _client.SendAsync(history);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.Equal("Cancelled", await ReadStringPropertyAsync(historyResponse, "status"));
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrowId);
        Assert.NotNull(escrow.CancellationTransactionHash);
        Assert.Equal(wasFunded, escrow.FundingTransactionHash is not null);
        Assert.Null(escrow.CancellationNoticeReadAt);
        Assert.Equal(ProjectStatus.Open, await db.Projects.Where(item => item.Id == projectId).Select(item => item.Status).SingleAsync());

        using var list = CreateAuthorizedRequest(HttpMethod.Get, "/api/projects/mine", client);
        using var listed = await _client.SendAsync(list);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        using var listDocument = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var row = Assert.Single(listDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(projectId, row.GetProperty("id").GetGuid());
        Assert.True(row.GetProperty("hasEscrowHistory").GetBoolean());
    }

    [Fact]
    public async Task DeleteProject_StillAllowsAnOpenProjectWithNoEscrowRecord()
    {
        var client = await RegisterAndLoginAsync("Client", "Draft project owner");
        var projectId = await CreateProjectAsync(client, DateTimeOffset.UtcNow.AddDays(30));
        using var list = CreateAuthorizedRequest(HttpMethod.Get, "/api/projects/mine", client);
        using var listed = await _client.SendAsync(list);
        using var document = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var row = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.False(row.GetProperty("hasEscrowHistory").GetBoolean());
        using var delete = CreateAuthorizedRequest(HttpMethod.Delete, $"/api/projects/{projectId}", client);
        using var response = await _client.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
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
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
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

    // Cancelling one escrow on a project shared with another freelancer
    // must retire only that freelancer: their stages go back on the market,
    // and everyone else keeps working.
    [Fact]
    public async Task Sync_CancellingOneEscrow_FreesOnlyThatFreelancersStages()
    {
        var clientAccessToken =
            await RegisterAndLoginAsync(
                role: "Client",
                fullName: "Split Cancellation Client"
            );

        var firstAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Split Cancellation Freelancer One"
            );

        var secondAccessToken =
            await RegisterAndLoginAsync(
                role: "Freelancer",
                fullName: "Split Cancellation Freelancer Two"
            );

        await ConnectWalletAsync(clientAccessToken);
        await ConnectWalletAsync(firstAccessToken);
        await ConnectWalletAsync(secondAccessToken);

        var deadline = DateTimeOffset.UtcNow.AddDays(14);

        var projectId = await CreateProjectAsync(
            clientAccessToken,
            deadline
        );

        // The two stages add up to the 10,000 budget, which acceptance
        // requires.
        var firstStageId = await CreateSplitMilestoneAsync(
            clientAccessToken,
            projectId,
            "First stage",
            6_000m,
            1,
            deadline.AddDays(-7)
        );

        var secondStageId = await CreateSplitMilestoneAsync(
            clientAccessToken,
            projectId,
            "Second stage",
            4_000m,
            2,
            deadline.AddDays(-3)
        );

        var firstProposalId = await CreateProposalAsync(
            firstAccessToken,
            projectId,
            firstStageId
        );

        await AcceptProposalAsync(
            clientAccessToken,
            projectId,
            firstProposalId
        );

        var secondProposalId = await CreateProposalAsync(
            secondAccessToken,
            projectId,
            secondStageId
        );

        await AcceptProposalAsync(
            clientAccessToken,
            projectId,
            secondProposalId
        );

        var participants =
            await GetEscrowParticipantsAsync(
                clientAccessToken,
                projectId
            );

        Assert.Equal(2, participants.Count);

        var firstFreelancerId = participants
            .Single(participant =>
                participant.AssignedTotal == 6_000m)
            .FreelancerId;

        var secondFreelancerId = participants
            .Single(participant =>
                participant.AssignedTotal == 4_000m)
            .FreelancerId;

        await AssertClientAssignmentsAsync(clientAccessToken, projectId, firstFreelancerId, secondFreelancerId);

        foreach (var freelancerId in new[]
                 {
                     firstFreelancerId,
                     secondFreelancerId
                 })
        {
            await CreateEscrowForAsync(
                clientAccessToken,
                projectId,
                freelancerId
            );

            await DeployEscrowForAsync(
                clientAccessToken,
                projectId,
                freelancerId
            );
        }

        var firstContractAddress =
            await GetEscrowContractAddressAsync(
                clientAccessToken,
                projectId,
                firstFreelancerId
            );

        FakeEscrowChainDeployer.MarkCancelled(
            firstContractAddress
        );

        using (var syncRequest = CreateAuthorizedRequest(
                   HttpMethod.Post,
                   $"/api/projects/{projectId}/escrow/sync" +
                   $"?freelancerId={firstFreelancerId}",
                   clientAccessToken))
        {
            var syncResponse =
                await _client.SendAsync(syncRequest);

            Assert.Equal(
                HttpStatusCode.OK,
                syncResponse.StatusCode
            );

            Assert.Equal(
                "Cancelled",
                await ReadStringPropertyAsync(
                    syncResponse,
                    "status"
                )
            );
        }

        var cancellationNotice = Assert.Single(await CancellationNoticesAsync(firstAccessToken));
        await AssertClientAssignmentsAsync(clientAccessToken, projectId, secondFreelancerId);
        Assert.Empty(await CancellationNoticesAsync(secondAccessToken));
        using (var siblingRead = CreateAuthorizedRequest(
                   HttpMethod.Post,
                   $"/api/notifications/{cancellationNotice.GetProperty("id").GetString()}/read",
                   secondAccessToken))
        {
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(siblingRead)).StatusCode);
        }

        await using (var scope =
                     _factory.Services.CreateAsyncScope())
        {
            var database =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var freedStage = await database.Milestones
                .AsNoTracking()
                .SingleAsync(item => item.Id == firstStageId);

            Assert.Null(freedStage.FreelancerId);

            Assert.Equal(
                MileStoneStatus.Pending,
                freedStage.Status
            );

            var keptStage = await database.Milestones
                .AsNoTracking()
                .SingleAsync(item => item.Id == secondStageId);

            Assert.Equal(
                secondFreelancerId,
                keptStage.FreelancerId
            );

            // The freelancer who was dropped is no longer hired.
            var firstProposal = await database.Proposals
                .AsNoTracking()
                .SingleAsync(item => item.Id == firstProposalId);

            Assert.Equal(
                ProposalStatus.Rejected,
                firstProposal.Status
            );

            var secondProposal = await database.Proposals
                .AsNoTracking()
                .SingleAsync(item => item.Id == secondProposalId);

            Assert.Equal(
                ProposalStatus.Accepted,
                secondProposal.Status
            );

            // Someone is still working, so the project is still running.
            var project = await database.Projects
                .AsNoTracking()
                .SingleAsync(item => item.Id == projectId);

            Assert.Equal(
                ProjectStatus.InProgress,
                project.Status
            );

            var secondEscrow = await database.Escrows
                .AsNoTracking()
                .SingleAsync(item =>
                    item.ProjectId == projectId &&
                    item.FreelancerId == secondFreelancerId);

            Assert.Equal(
                EscrowStatus.AwaitingFunding,
                secondEscrow.Status
            );
        }

        // Both contracts remain visible, including the departed freelancer.
        var remaining =
            await GetEscrowParticipantsAsync(
                clientAccessToken,
                projectId
            );

        Assert.Equal(2, remaining.Count);
        Assert.Contains(remaining, item => item.FreelancerId == firstFreelancerId && item.AssignedTotal == 0m);
        Assert.Contains(remaining, item => item.FreelancerId == secondFreelancerId && item.AssignedTotal == 4_000m);

        // Cancelling the last live escrow reopens the project.
        var secondContractAddress =
            await GetEscrowContractAddressAsync(
                clientAccessToken,
                projectId,
                secondFreelancerId
            );

        FakeEscrowChainDeployer.MarkCancelled(
            secondContractAddress
        );

        using (var syncRequest = CreateAuthorizedRequest(
                   HttpMethod.Post,
                   $"/api/projects/{projectId}/escrow/sync" +
                   $"?freelancerId={secondFreelancerId}",
                   clientAccessToken))
        {
            var syncResponse =
                await _client.SendAsync(syncRequest);

            Assert.Equal(
                HttpStatusCode.OK,
                syncResponse.StatusCode
            );
        }

        await using (var scope =
                     _factory.Services.CreateAsyncScope())
        {
            var database =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var project = await database.Projects
                .AsNoTracking()
                .SingleAsync(item => item.Id == projectId);

            Assert.Equal(
                ProjectStatus.Open,
                project.Status
            );

            var stillAssigned = await database.Milestones
                .AsNoTracking()
                .AnyAsync(item =>
                    item.ProjectId == projectId &&
                    item.FreelancerId != null);

            Assert.False(stillAssigned);
        }
        await AssertClientAssignmentsAsync(clientAccessToken, projectId);
    }

    private async Task AssertClientAssignmentsAsync(string token, Guid projectId, params Guid[] expected)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/projects/mine?pageSize=100", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var project = payload.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == projectId);
        var assigned = project.GetProperty("assignedFreelancers").EnumerateArray().ToList();
        Assert.Equal(expected.Order(), assigned.Select(item => item.GetProperty("id").GetGuid()).Order());
        Assert.All(assigned, item => Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("fullName").GetString())));
    }

    [Fact]
    public async Task ContractIdentity_AllowsSameAddressOnDifferentChains_RejectsDuplicateOnSameChain()
    {
        var client = await RegisterAndLoginAsync("Client", "Chain Identity Client");
        var freelancer = await RegisterAndLoginAsync("Freelancer", "Chain Identity Freelancer");
        await ConnectWalletAsync(client);
        await ConnectWalletAsync(freelancer);
        var firstProject = await CreateInProgressProjectAsync(client, freelancer);
        var secondProject = await CreateInProgressProjectAsync(client, freelancer);
        await CreateEscrowAsync(client, firstProject);
        await CreateEscrowAsync(client, secondProject);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db.Escrows.SingleAsync(item => item.ProjectId == firstProject);
        var second = await db.Escrows.SingleAsync(item => item.ProjectId == secondProject);
        var address = "0x" + Guid.NewGuid().ToString("N") + "12345678";
        first.ContractAddress = address;
        second.ContractAddress = address;
        second.ChainId = 11_155_111;
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.Escrows.CountAsync(item => item.ContractAddress == address));

        second.ChainId = first.ChainId;
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sync_PreservesTransactionHistoryAndAccessAfterCancellation(bool wasFunded)
    {
        var client = await RegisterAndLoginAsync("Client", "History Client");
        var freelancer = await RegisterAndLoginAsync("Freelancer", "History Freelancer");
        var outsider = await RegisterAndLoginAsync("Freelancer", "History Outsider");
        await ConnectWalletAsync(client);
        await ConnectWalletAsync(freelancer);
        var project = await CreateInProgressProjectAsync(client, freelancer);
        await CreateEscrowAsync(client, project);
        await DeployEscrowAsync(client, project);
        using (var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{project}/escrow", client))
        {
            var response = await _client.SendAsync(request);
            var address = await ReadStringPropertyAsync(response, "contractAddress");
            FakeEscrowChainDeployer.MarkCancelled(address, wasFunded);
        }
        string? fundingHash = null, cancellationHash = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{project}/escrow/sync", client);
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var row = payload.RootElement;
            Assert.Equal("Cancelled", row.GetProperty("status").GetString());
            Assert.Equal(wasFunded, row.GetProperty("fundingTransactionHash").ValueKind == JsonValueKind.String);
            Assert.Matches("^0x[0-9a-f]{64}$", row.GetProperty("cancellationTransactionHash").GetString()!);
            if (attempt == 0)
            {
                fundingHash = row.GetProperty("fundingTransactionHash").GetString();
                cancellationHash = row.GetProperty("cancellationTransactionHash").GetString();
            }
            else
            {
                Assert.Equal(fundingHash, row.GetProperty("fundingTransactionHash").GetString());
                Assert.Equal(cancellationHash, row.GetProperty("cancellationTransactionHash").GetString());
            }
        }
        foreach (var token in new[] { client, freelancer })
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{project}/escrows", token);
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var row = Assert.Single(payload.RootElement.EnumerateArray());
            Assert.Equal(0, row.GetProperty("milestoneCount").GetInt32());
            Assert.Equal("Cancelled", row.GetProperty("escrow").GetProperty("status").GetString());
        }
        using (var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{project}/workspace", freelancer))
        {
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Empty(payload.RootElement.GetProperty("milestones").EnumerateArray());
        }
        foreach (var suffix in new[] { "escrows", "workspace" })
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, $"/api/projects/{project}/{suffix}", outsider);
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(request)).StatusCode);
        }
        using (var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/projects/assigned-to-me?pageSize=100", freelancer))
        {
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var row = Assert.Single(payload.RootElement.GetProperty("items").EnumerateArray());
            Assert.True(row.GetProperty("isHistorical").GetBoolean());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationNotice_IsDurablePrivateAndAcknowledgedOnce(bool wasFunded)
    {
        var client = await RegisterAndLoginAsync("Client", "Notice Client");
        var freelancer = await RegisterAndLoginAsync("Freelancer", "Notice Freelancer");
        var outsider = await RegisterAndLoginAsync("Freelancer", "Notice Outsider");
        await ConnectWalletAsync(client);
        await ConnectWalletAsync(freelancer);
        var project = await CreateInProgressProjectAsync(client, freelancer);
        await CreateEscrowAsync(client, project);
        await DeployEscrowAsync(client, project);

        Guid escrowId;
        string address;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var escrow = await db.Escrows.AsNoTracking().SingleAsync(item => item.ProjectId == project);
            escrowId = escrow.Id;
            address = escrow.ContractAddress!;
        }

        var noticeId = $"escrow-cancelled:{escrowId}";
        var readPath = $"/api/notifications/{Uri.EscapeDataString(noticeId)}/read";
        Assert.Empty(await CancellationNoticesAsync(freelancer));
        using (var premature = CreateAuthorizedRequest(HttpMethod.Post, readPath, freelancer))
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(premature)).StatusCode);

        // A wallet transaction alone cannot fabricate a backend notice; sync
        // verifies the chain evidence and commits cancellation/assignments.
        FakeEscrowChainDeployer.MarkCancelled(address, wasFunded);
        Assert.Empty(await CancellationNoticesAsync(freelancer));
        using (var first = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{project}/escrow/sync", client))
        using (var concurrent = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{project}/escrow/sync", freelancer))
        {
            var responses = await Task.WhenAll(_client.SendAsync(first), _client.SendAsync(concurrent));
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }

        // No chat thread is required. Cleared stages and an offline recipient
        // do not lose the event, and polling never duplicates it.
        for (var poll = 0; poll < 2; poll++)
        {
            var notice = Assert.Single(await CancellationNoticesAsync(freelancer));
            Assert.Equal(noticeId, notice.GetProperty("id").GetString());
            Assert.True(notice.GetProperty("canMarkRead").GetBoolean());
            Assert.Equal($"/dashboard/freelancer/projects/{project}", notice.GetProperty("href").GetString());
            Assert.Contains(wasFunded ? "returned to the client without a fee" : "not funded",
                notice.GetProperty("message").GetString());
        }
        using (var unread = CreateAuthorizedRequest(HttpMethod.Get, "/api/notifications", freelancer))
        {
            using var payload = JsonDocument.Parse(await (await _client.SendAsync(unread)).Content.ReadAsStringAsync());
            Assert.Equal(1, payload.RootElement.GetProperty("actionCount").GetInt32());
        }

        // Legacy cancellation rows can predate lifecycle-event tracing. Do
        // not tell their recipient that funding/refunding is known until it
        // has evidence; later backfill must preserve the acknowledgement.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Escrows.Where(item => item.Id == escrowId).ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.FundingTransactionHash, (string?)null)
                .SetProperty(item => item.CancellationTransactionHash, (string?)null));
        }
        var legacy = Assert.Single(await CancellationNoticesAsync(freelancer));
        Assert.DoesNotContain("not funded", legacy.GetProperty("message").GetString());
        Assert.DoesNotContain("returned to the client", legacy.GetProperty("message").GetString());
        Assert.Empty(await CancellationNoticesAsync(client));
        Assert.Empty(await CancellationNoticesAsync(outsider));
        using (var forbidden = CreateAuthorizedRequest(HttpMethod.Post, readPath, client))
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(forbidden)).StatusCode);
        using (var unrelated = CreateAuthorizedRequest(HttpMethod.Post, readPath, outsider))
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(unrelated)).StatusCode);
        using (var unknown = CreateAuthorizedRequest(HttpMethod.Post, $"/api/notifications/escrow-cancelled:{Guid.NewGuid()}/read", freelancer))
            Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(unknown)).StatusCode);
        using (var anonymous = new HttpRequestMessage(HttpMethod.Post, readPath))
            Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonymous)).StatusCode);

        DateTimeOffset? readAt = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var read = CreateAuthorizedRequest(HttpMethod.Post, readPath, freelancer);
            Assert.Equal(HttpStatusCode.NoContent, (await _client.SendAsync(read)).StatusCode);
            Assert.Empty(await CancellationNoticesAsync(freelancer));
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var persisted = await db.Escrows.AsNoTracking().SingleAsync(item => item.Id == escrowId);
            Assert.NotNull(persisted.CancellationNoticeReadAt);
            if (attempt == 0) readAt = persisted.CancellationNoticeReadAt;
            else Assert.Equal(readAt, persisted.CancellationNoticeReadAt);
        }
        using (var retry = CreateAuthorizedRequest(HttpMethod.Post, $"/api/projects/{project}/escrow/sync", client))
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(retry)).StatusCode);
        Assert.Empty(await CancellationNoticesAsync(freelancer));
        using (var caughtUp = CreateAuthorizedRequest(HttpMethod.Get, "/api/notifications", freelancer))
        {
            using var payload = JsonDocument.Parse(await (await _client.SendAsync(caughtUp)).Content.ReadAsStringAsync());
            Assert.Equal(0, payload.RootElement.GetProperty("actionCount").GetInt32());
        }
    }

    [Theory]
    [InlineData(MileStoneStatus.Rejected)]
    [InlineData(MileStoneStatus.Submitted)]
    public async Task WorkflowNotices_UseOnlyOwnStagesAndRespectDeliveryOrder(MileStoneStatus firstStatus)
    {
        var client = await RegisterAndLoginAsync("Client", "Stage Notice Client");
        var first = await RegisterAndLoginAsync("Freelancer", "Stage Notice First");
        var sibling = await RegisterAndLoginAsync("Freelancer", "Stage Notice Sibling");
        foreach (var token in new[] { client, first, sibling }) await ConnectWalletAsync(token);
        var deadline = DateTimeOffset.UtcNow.AddDays(14);
        var project = await CreateProjectAsync(client, deadline);
        var firstStage = await CreateSplitMilestoneAsync(client, project, "First private stage", 3000m, 1, deadline.AddDays(-7));
        var siblingStage = await CreateSplitMilestoneAsync(client, project, "Sibling private stage", 4000m, 2, deadline.AddDays(-5));
        var lastStage = await CreateSplitMilestoneAsync(client, project, "Later first stage", 3000m, 3, deadline.AddDays(-3));
        await AcceptProposalAsync(client, project, await CreateProposalAsync(first, project, firstStage, lastStage));
        await AcceptProposalAsync(client, project, await CreateProposalAsync(sibling, project, siblingStage));
        var participants = await GetEscrowParticipantsAsync(client, project);
        foreach (var participant in participants)
        {
            await CreateEscrowForAsync(client, project, participant.FreelancerId);
            await DeployEscrowForAsync(client, project, participant.FreelancerId);
            using var sync = CreateAuthorizedRequest(HttpMethod.Post,
                $"/api/projects/{project}/escrow/sync?freelancerId={participant.FreelancerId}", client);
            Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(sync)).StatusCode);
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Milestones.Where(item => item.Id == firstStage)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, firstStatus));
            await db.Milestones.Where(item => item.Id == siblingStage)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, MileStoneStatus.Rejected));
        }
        foreach (var (token, ownStage, otherStage) in new[]
                 { (first, firstStage, siblingStage), (sibling, siblingStage, firstStage) })
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/notifications", token);
            var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.DoesNotContain(items, item => item.GetProperty("id").GetString()!.Contains(otherStage.ToString()));
            Assert.DoesNotContain(items, item => item.GetProperty("kind").GetString() == "MilestoneReadyToStart");
            if (token == sibling || firstStatus == MileStoneStatus.Rejected)
                Assert.Contains(items, item => item.GetProperty("id").GetString() == $"milestone-changes:{ownStage}");
        }
    }

    private async Task<List<JsonElement>> CancellationNoticesAsync(string token)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, "/api/notifications", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("items").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() == "EscrowCancelled")
            .Select(item => item.Clone()).ToList();
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("client")]
    [InlineData("missing")]
    [InlineData("cancellation")]
    [InlineData("chain")]
    public async Task Sync_RejectsMismatchedChainEvidenceWithoutChangingFinancialState(string mismatch)
    {
        var client = await RegisterAndLoginAsync("Client", "Evidence Client");
        var freelancer = await RegisterAndLoginAsync("Freelancer", "Evidence Freelancer");
        var wallet = await ConnectWalletAsync(client);
        await ConnectWalletAsync(freelancer);
        var project = await CreateInProgressProjectAsync(client, freelancer);
        await CreateEscrowAsync(client, project);
        await DeployEscrowAsync(client, project);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var escrow = await db.Escrows.SingleAsync(item => item.ProjectId == project);
        if (mismatch == "chain")
        {
            escrow.ChainId = 11_155_111;
            await db.SaveChangesAsync();
        }
        else
        {
            var funding = mismatch == "missing" ? null : new OnChainFundingEvent(
                "0x" + new string('a', 64),
                mismatch == "client" ? "0x" + new string('b', 40) : wallet,
                TokenAmountConverter.ToBaseUnits(escrow.TotalAmount, 6) + (mismatch == "amount" ? 1 : 0));
            FakeEscrowChainDeployer.SetLifecycleEvents(escrow.ContractAddress!,
                new OnChainLifecycleEvents(funding, mismatch == "cancellation"
                    ? new OnChainCancellationEvent("0x" + new string('c', 64), 1) : null));
            if (mismatch == "cancellation") FakeEscrowChainDeployer.MarkCancelled(escrow.ContractAddress!);
        }
        var sync = scope.ServiceProvider.GetRequiredService<IEscrowChainSyncOrchestrator>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => sync.TrySyncAsync(escrow.Id, CancellationToken.None));
        await db.Entry(escrow).ReloadAsync();
        Assert.Equal(EscrowStatus.AwaitingFunding, escrow.Status);
        Assert.Null(escrow.FundingTransactionHash);
        Assert.Null(escrow.CancellationTransactionHash);
        Assert.NotNull((await db.Milestones.SingleAsync(item => item.ProjectId == project)).FreelancerId);
    }

    private sealed record EscrowParticipant(
        Guid FreelancerId,
        decimal AssignedTotal);

    private async Task<List<EscrowParticipant>>
        GetEscrowParticipantsAsync(
            string accessToken,
            Guid projectId)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/escrows",
            accessToken
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        using var payload = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );

        return payload.RootElement
            .EnumerateArray()
            .Select(element => new EscrowParticipant(
                element.GetProperty("freelancerId")
                    .GetGuid(),
                element.GetProperty("assignedTotal")
                    .GetDecimal()
            ))
            .ToList();
    }

    private async Task<Guid> CreateSplitMilestoneAsync(
        string accessToken,
        Guid projectId,
        string title,
        decimal amount,
        int sequenceNumber,
        DateTimeOffset deadline)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/milestones",
            accessToken,
            new
            {
                Title = title,

                Description =
                    $"{title} of a project split between two freelancers.",

                Amount = amount,

                SequenceNumber = sequenceNumber,

                Deadline = deadline
            }
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );

        return await ReadGuidPropertyAsync(
            response,
            "id"
        );
    }

    private async Task CreateEscrowForAsync(
        string clientAccessToken,
        Guid projectId,
        Guid freelancerId)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/escrow",
            clientAccessToken,
            new
            {
                FreelancerId = freelancerId,

                ChainId = 31337,

                TokenAddress =
                    "0x5FbDB2315678afecb367f032d93F642f64180aa3"
            }
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode
        );
    }

    private async Task DeployEscrowForAsync(
        string clientAccessToken,
        Guid projectId,
        Guid freelancerId)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"/api/projects/{projectId}/escrow/deploy" +
            $"?freelancerId={freelancerId}",
            clientAccessToken
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );
    }

    private async Task<string>
        GetEscrowContractAddressAsync(
            string accessToken,
            Guid projectId,
            Guid freelancerId)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/escrow" +
            $"?freelancerId={freelancerId}",
            accessToken
        );

        var response = await _client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode
        );

        return await ReadStringPropertyAsync(
            response,
            "contractAddress"
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
        await TestAuth.ConfirmEmailAsync(_client, email);

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

        var milestoneId = await CreateMilestoneAsync(
            clientAccessToken,
            projectId,
            projectDeadline.AddDays(-5)
        );

        var proposalId =
            await CreateProposalAsync(
                freelancerAccessToken,
                projectId,
                milestoneId
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

    private async Task<Guid> CreateMilestoneAsync(
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

        return await ReadGuidPropertyAsync(
            response,
            "id"
        );
    }

    private async Task<Guid> CreateProposalAsync(
        string accessToken,
        Guid projectId,
        params Guid[] milestoneIds)
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

                    // The price is the sum of the milestones claimed,
                    // so the proposal names them instead of a bid.
                    MilestoneIds = milestoneIds,

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
