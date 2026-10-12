using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Nethereum.Signer;
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

    // A project can be split between freelancers, so completion has two
    // different conditions: an escrow is finished once its own freelancer's
    // stages are approved, while the project waits for every stage. Before
    // this was separated, one stage nobody had taken left a fully paid
    // escrow stuck on Funded forever.
    [Fact]
    public async Task ApprovingAFreelancersLastStage_CompletesTheirEscrow_ButNotAPartlyStaffedProject()
    {
        var uniqueValue = Guid.NewGuid().ToString("N");
        const string password = "Testpass1";
        var clientEmail = $"split-client-{uniqueValue}@trustflow.test";
        var freelancerEmail = $"split-freelancer-{uniqueValue}@trustflow.test";

        await RegisterAsync(clientEmail, password, "Split Client", "Client");
        await RegisterAsync(
            freelancerEmail,
            password,
            "Split Freelancer",
            "Freelancer"
        );

        var clientToken = await LoginAsync(clientEmail, password);
        var freelancerToken = await LoginAsync(freelancerEmail, password);

        var projectDeadline = DateTimeOffset.UtcNow.AddDays(90);
        var projectId = await CreateProjectAsync(
            clientToken,
            projectDeadline
        );

        var minePaidStage = await CreateMilestoneAsync(
            projectId,
            clientToken,
            title: "Design",
            amount: 1000.00m,
            deadline: DateTimeOffset.UtcNow.AddDays(30),
            sequenceNumber: 1
        );

        // Nobody applies for this stage, so it stays unassigned and the
        // project cannot finish.
        await CreateMilestoneAsync(
            projectId,
            clientToken,
            title: "Back end",
            amount: 1500.00m,
            deadline: DateTimeOffset.UtcNow.AddDays(60),
            sequenceNumber: 2
        );

        var proposalId = await CreateProposalAsync(
            projectId,
            freelancerToken,
            minePaidStage
        );

        await AcceptProposalAsync(projectId, proposalId, clientToken);

        await ConnectWalletAsync(clientToken);
        await ConnectWalletAsync(freelancerToken);

        var contractAddress = await SetUpFundedEscrowAsync(
            projectId,
            clientToken
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            minePaidStage,
            "start",
            freelancerToken,
            HttpStatusCode.OK
        );

        await ChangeMilestoneStatusAsync(
            projectId,
            minePaidStage,
            "submit",
            freelancerToken,
            HttpStatusCode.OK
        );

        FakeEscrowChainDeployer.MarkMilestoneReleased(contractAddress, 0);

        await ChangeMilestoneStatusAsync(
            projectId,
            minePaidStage,
            "approve",
            clientToken,
            HttpStatusCode.OK
        );

        using var escrowRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/escrow",
            clientToken
        );

        var escrowResponse = await _client.SendAsync(escrowRequest);
        Assert.Equal(HttpStatusCode.OK, escrowResponse.StatusCode);

        var escrow = await escrowResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // The freelancer delivered everything they took, so their contract
        // is closed and fully released.
        Assert.Equal("Completed", escrow.GetProperty("status").GetString());
        Assert.Equal(
            1000.00m,
            escrow.GetProperty("releasedAmount").GetDecimal()
        );
        Assert.Equal(
            1000.00m,
            escrow.GetProperty("totalAmount").GetDecimal()
        );

        using var workspaceRequest = CreateAuthorizedRequest(
            HttpMethod.Get,
            $"/api/projects/{projectId}/workspace",
            clientToken
        );

        var workspaceResponse = await _client.SendAsync(workspaceRequest);
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);

        var workspace = await workspaceResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // The unassigned stage is still outstanding, so the project is not
        // finished and stays open to proposals for it.
        Assert.Equal(
            "InProgress",
            workspace.GetProperty("status").GetString()
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
                freelancerToken,
                milestoneOneId,
                milestoneTwoId
            );

        await AcceptProposalAsync(
            projectId,
            proposalId,
            clientToken
        );

        await ConnectWalletAsync(clientToken);
        await ConnectWalletAsync(freelancerToken);
        var contractAddress = await SetUpFundedEscrowAsync(
            projectId,
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

        using (var submissionsRequest = CreateAuthorizedRequest(
                   HttpMethod.Get,
                   $"/api/projects/{projectId}/milestones/{milestoneOneId}/submissions",
                   clientToken))
        {
            var submissionsResponse = await _client.SendAsync(submissionsRequest);
            Assert.Equal(HttpStatusCode.OK, submissionsResponse.StatusCode);
            using var submissionsJson = JsonDocument.Parse(
                await submissionsResponse.Content.ReadAsStringAsync());
            var firstSubmission = submissionsJson.RootElement[0];
            Assert.Equal(1, firstSubmission.GetProperty("version").GetInt32());
            Assert.Equal("Completed work for client review.",
                firstSubmission.GetProperty("note").GetString());
            var attachment = firstSubmission.GetProperty("attachments")[0];
            using var downloadRequest = CreateAuthorizedRequest(
                HttpMethod.Get,
                $"/api/projects/{projectId}/milestones/{milestoneOneId}/submissions/" +
                $"{firstSubmission.GetProperty("id").GetGuid()}/files/{attachment.GetProperty("id").GetGuid()}",
                clientToken);
            var downloadResponse = await _client.SendAsync(downloadRequest);
            Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
            Assert.NotEmpty(await downloadResponse.Content.ReadAsByteArrayAsync());
        }

        using (var changesRequest = CreateAuthorizedRequest(
                   HttpMethod.Patch,
                   $"/api/projects/{projectId}/milestones/{milestoneOneId}/request-changes",
                   clientToken))
        {
            changesRequest.Content = JsonContent.Create(new
            {
                reason = "Please update the final color and submit again."
            });
            Assert.Equal(HttpStatusCode.OK,
                (await _client.SendAsync(changesRequest)).StatusCode);
        }

        await ChangeMilestoneStatusAsync(projectId, milestoneOneId, "start",
            freelancerToken, HttpStatusCode.OK);
        await ChangeMilestoneStatusAsync(projectId, milestoneOneId, "submit",
            freelancerToken, HttpStatusCode.OK);

        using (var versionsRequest = CreateAuthorizedRequest(
                   HttpMethod.Get,
                   $"/api/projects/{projectId}/milestones/{milestoneOneId}/submissions",
                   clientToken))
        {
            var versionsResponse = await _client.SendAsync(versionsRequest);
            using var versionsJson = JsonDocument.Parse(
                await versionsResponse.Content.ReadAsStringAsync());
            Assert.Equal(2, versionsJson.RootElement.GetArrayLength());
            Assert.Equal(2, versionsJson.RootElement[0].GetProperty("version").GetInt32());
            Assert.Contains("final color",
                versionsJson.RootElement[1].GetProperty("clientFeedback").GetString());
        }

        using var earlyApproveRequest =
            CreateAuthorizedRequest(
                HttpMethod.Patch,
                $"/api/projects/{projectId}" +
                $"/milestones/{milestoneOneId}/approve",
                clientToken
            );

        var earlyApproveResponse =
            await _client.SendAsync(earlyApproveRequest);

        Assert.Equal(
            HttpStatusCode.Conflict,
            earlyApproveResponse.StatusCode
        );

        var earlyApproveBody =
            await earlyApproveResponse.Content
                .ReadAsStringAsync();

        Assert.Contains(
            "has not released this milestone on-chain",
            earlyApproveBody,
            StringComparison.OrdinalIgnoreCase
        );

        FakeEscrowChainDeployer.MarkMilestoneReleased(
            contractAddress,
            0
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

        FakeEscrowChainDeployer.MarkMilestoneReleased(
            contractAddress,
            1
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

    private async Task ConnectWalletAsync(
        string accessToken)
    {
        var walletKey = EthECKey.GenerateKey();
        var walletAddress = walletKey.GetPublicAddress();

        using var challengeRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/wallet/challenge",
                accessToken,
                new { WalletAddress = walletAddress }
            );

        var challengeResponse =
            await _client.SendAsync(challengeRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            challengeResponse.StatusCode
        );

        await using var challengeStream =
            await challengeResponse.Content
                .ReadAsStreamAsync();

        using var challengeDocument =
            await JsonDocument.ParseAsync(challengeStream);

        var message = challengeDocument.RootElement
            .GetProperty("message")
            .GetString()!;

        var signature = new EthereumMessageSigner()
            .EncodeUTF8AndSign(message, walletKey);

        using var verifyRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                "/api/wallet/verify",
                accessToken,
                new { Signature = signature }
            );

        var verifyResponse =
            await _client.SendAsync(verifyRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            verifyResponse.StatusCode
        );
    }

    private async Task<string> SetUpFundedEscrowAsync(
        Guid projectId,
        string clientToken)
    {
        using var createRequest =
            CreateAuthorizedJsonRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow",
                clientToken,
                new
                {
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
                }
            );

        var createResponse =
            await _client.SendAsync(createRequest);

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode
        );

        using var deployRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/deploy",
                clientToken
            );

        var deployResponse =
            await _client.SendAsync(deployRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            deployResponse.StatusCode
        );

        using var syncRequest =
            CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/escrow/sync",
                clientToken
            );

        var syncResponse =
            await _client.SendAsync(syncRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            syncResponse.StatusCode
        );

        await using var deployResponseStream =
            await deployResponse.Content
                .ReadAsStreamAsync();

        using var deployDocument =
            await JsonDocument.ParseAsync(
                deployResponseStream
            );

        var contractAddress = deployDocument.RootElement
            .GetProperty("contractAddress")
            .GetString()!;

        FakeEscrowChainDeployer.MarkWorkStarted(contractAddress);

        return contractAddress;
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
        string freelancerToken,
        params Guid[] milestoneIds)
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

                    // The price is the sum of the milestones claimed,
                    // so the proposal names them instead of a bid.
                    MilestoneIds = milestoneIds,

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
        if (action == "submit")
        {
            using var submitRequest = CreateAuthorizedRequest(
                HttpMethod.Post,
                $"/api/projects/{projectId}/milestones/{milestoneId}/submissions",
                accessToken);
            submitRequest.Content = new MultipartFormDataContent
            {
                { new StringContent("Completed work for client review."), "note" },
                { new StringContent("https://example.test/delivery"), "previewLink" }
            };
            var image = new ByteArrayContent(Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
            image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            ((MultipartFormDataContent)submitRequest.Content).Add(image, "files", "delivery.png");
            var submitResponse = await _client.SendAsync(submitRequest);
            Assert.Equal(expectedStatusCode, submitResponse.StatusCode);
            return;
        }

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
