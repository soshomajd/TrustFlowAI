using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Nethereum.Signer;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed record Person(
    string Email,
    string FullName,
    string Token,
    Guid Id);

public sealed record SubmittedMilestone(
    Person Client,
    Person Freelancer,
    Guid ProjectId,
    Guid MilestoneId,
    string ContractAddress);

// Builds the situation every AI feature starts from: a project in progress,
// a funded escrow whose work has started, and one milestone delivered and
// waiting for the client. It goes through the real API the whole way.
public static class AiTestScenario
{
    public const string Password = "Testpass1";

    public static async Task<Person> NewPersonAsync(
        HttpClient client,
        string role,
        string name)
    {
        var email = $"ai-{role.ToLowerInvariant()}-{Guid.NewGuid():N}" +
                    "@trustflow.test";

        var registered = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { FullName = name, Email = email, Password, Role = role });

        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        await TestAuth.ConfirmEmailAsync(client, email);

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Email = email, Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<JsonElement>();

        return new Person(
            email,
            name,
            body.GetProperty("accessToken").GetString()!,
            body.GetProperty("user").GetProperty("id").GetGuid());
    }

    public static HttpRequestMessage Request(
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    public static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string token,
        object? body = null)
    {
        using var request = Request(method, url, token, body);

        return await client.SendAsync(request);
    }

    public static async Task<JsonElement> ReadAsync(
        HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    public static async Task<SubmittedMilestone> CreateSubmittedMilestoneAsync(
        HttpClient client,
        string note = "The work is finished and attached.")
    {
        var clientPerson = await NewPersonAsync(
            client, "Client", "Dispute Client");

        var freelancer = await NewPersonAsync(
            client, "Freelancer", "Dispute Freelancer");

        var project = await ReadAsync(await SendAsync(
            client, HttpMethod.Post, "/api/projects", clientPerson.Token,
            new
            {
                Title = "Build a landing page",
                Description = "A one page site with a contact form.",
                Budget = 1000m,
                Deadline = DateTimeOffset.UtcNow.AddDays(90)
            }));

        var projectId = project.GetProperty("id").GetGuid();

        var milestone = await ReadAsync(await SendAsync(
            client, HttpMethod.Post,
            $"/api/projects/{projectId}/milestones", clientPerson.Token,
            new
            {
                Title = "Landing page",
                Description =
                    "Responsive landing page with a working contact form.",
                Amount = 1000m,
                SequenceNumber = 1,
                Deadline = DateTimeOffset.UtcNow.AddDays(30)
            }));

        var milestoneId = milestone.GetProperty("id").GetGuid();

        var proposal = await ReadAsync(await SendAsync(
            client, HttpMethod.Post,
            $"/api/projects/{projectId}/proposals", freelancer.Token,
            new
            {
                CoverLetter = "I can build this.",
                EstimatedDays = 14,
                MilestoneIds = new[] { milestoneId }
            }));

        var accepted = await SendAsync(
            client, HttpMethod.Patch,
            $"/api/projects/{projectId}/proposals/" +
            $"{proposal.GetProperty("id").GetGuid()}/accept",
            clientPerson.Token);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        await ConnectWalletAsync(client, clientPerson.Token);
        await ConnectWalletAsync(client, freelancer.Token);

        var escrowPath = $"/api/projects/{projectId}/escrow";

        Assert.Equal(
            HttpStatusCode.Created,
            (await SendAsync(
                client, HttpMethod.Post, escrowPath, clientPerson.Token,
                new
                {
                    ChainId = 31337,
                    TokenAddress =
                        "0x5FbDB2315678afecb367f032d93F642f64180aa3"
                })).StatusCode);

        var deployed = await SendAsync(
            client, HttpMethod.Post, $"{escrowPath}/deploy",
            clientPerson.Token);

        Assert.Equal(HttpStatusCode.OK, deployed.StatusCode);

        var contractAddress = (await ReadAsync(deployed))
            .GetProperty("contractAddress").GetString()!;

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(
                client, HttpMethod.Post, $"{escrowPath}/sync",
                clientPerson.Token)).StatusCode);

        FakeEscrowChainDeployer.MarkWorkStarted(contractAddress);

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(
                client, HttpMethod.Patch,
                $"/api/projects/{projectId}/milestones/{milestoneId}/start",
                freelancer.Token)).StatusCode);

        using var submit = Request(
            HttpMethod.Post,
            $"/api/projects/{projectId}/milestones/{milestoneId}/submissions",
            freelancer.Token);

        var form = new MultipartFormDataContent
        {
            { new StringContent(note), "note" },
            { new StringContent("https://example.test/delivery"), "previewLink" }
        };

        var image = new ByteArrayContent(Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));

        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image, "files", "delivery.png");
        submit.Content = form;

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.SendAsync(submit)).StatusCode);

        return new SubmittedMilestone(
            clientPerson,
            freelancer,
            projectId,
            milestoneId,
            contractAddress);
    }

    private static async Task ConnectWalletAsync(
        HttpClient client,
        string token)
    {
        var key = EthECKey.GenerateKey();

        var challenge = await ReadAsync(await SendAsync(
            client, HttpMethod.Post, "/api/wallet/challenge", token,
            new { WalletAddress = key.GetPublicAddress() }));

        var signature = new EthereumMessageSigner().EncodeUTF8AndSign(
            challenge.GetProperty("message").GetString()!, key);

        Assert.Equal(
            HttpStatusCode.OK,
            (await SendAsync(
                client, HttpMethod.Post, "/api/wallet/verify", token,
                new { Signature = signature })).StatusCode);
    }
}
