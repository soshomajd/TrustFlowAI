using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Nethereum.Signer;
using TrustFlow.Api.Blockchain;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Security;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class LocalAdminReviewTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Entry = "/api/auth/admin/local-preview";

    [Theory]
    [InlineData("Development", "31337", "http://127.0.0.1:8545", true)]
    [InlineData("Development", "31337", "http://localhost:8545", true)]
    [InlineData("Production", "31337", "http://127.0.0.1:8545", false)]
    [InlineData("Staging", "31337", "http://127.0.0.1:8545", false)]
    [InlineData("Testing", "31337", "http://127.0.0.1:8545", false)]
    [InlineData("Development", "1", "http://127.0.0.1:8545", false)]
    [InlineData("Development", "31337", "https://rpc.example.test", false)]
    [InlineData("Development", "31337", "http://user@localhost:8545", false)]
    public void ReviewRequiresOptInDevelopmentAndALocalTestChain(string environment, string chain, string rpc, bool allowed)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSecurity:LocalPreviewEnabled"] = "true", ["AdminSecurity:LocalReviewEnabled"] = "true",
            ["Blockchain:ChainId"] = chain, ["Blockchain:RpcUrl"] = rpc
        }).Build();
        Assert.Equal(allowed, LocalAdminPreview.ReviewEnabled(new LocalEnvironment(environment), settings));
        settings["AdminSecurity:LocalReviewEnabled"] = "false";
        Assert.False(LocalAdminPreview.ReviewEnabled(new LocalEnvironment(environment), settings));
    }

    [Fact]
    public async Task LocalReviewCanSaveAndSync_ButCannotGrantOtherWritesOrUpgradeOldTokens()
    {
        using var normal = Client(factory);
        var person = await NewPersonAsync(normal, "Client", "Local review owner");
        var scenario = await CreateSubmittedMilestoneAsync(normal);
        FakeEscrowChainDeployer.OpenCase(scenario.ContractAddress, OnChainCaseKind.PaymentRequest,
            DateTimeOffset.UtcNow.AddDays(-15));
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(normal, HttpMethod.Post,
            $"/api/projects/{scenario.ProjectId}/escrow/sync", scenario.Client.Token)).StatusCode);
        Guid caseId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(person.Id.ToString()))!;
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Admin)).Succeeded);
            user.EmailConfirmed = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            caseId = await db.EscrowCases.Where(item => item.Escrow.ProjectId == scenario.ProjectId)
                .Select(item => item.Id).SingleAsync();
        }

        using var application = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSecurity:LocalPreviewEnabled"] = "true", ["AdminSecurity:LocalPreviewEmail"] = person.Email,
                ["Review:AdminEmails:0"] = person.Email
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebHostEnvironment>();
                services.AddSingleton<IWebHostEnvironment>(new LocalEnvironment("Development"));
            });
        });
        using var client = Client(application);
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        var oldSession = await ReadAsync(await client.PostAsync(Entry, null));
        var oldToken = oldSession.GetProperty("accessToken").GetString()!;
        configuration["AdminSecurity:LocalReviewEnabled"] = "true";
        var notePath = $"/api/review/cases/{caseId}/note";
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Put,
            notePath, oldToken, new { Note = "An old preview must remain read-only." })).StatusCode);

        using var entry = await client.PostAsync(Entry, null);
        Assert.Equal(HttpStatusCode.OK, entry.StatusCode);
        Assert.False(entry.Headers.Contains("Set-Cookie"));
        var body = await ReadAsync(entry);
        Assert.False(body.GetProperty("readOnly").GetBoolean());
        Assert.True(body.GetProperty("localReview").GetBoolean());
        var token = body.GetProperty("accessToken").GetString()!;
        const string reason = "A saved local test decision, pending the human wallet transaction.";
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Put,
            notePath, token, new { Note = reason })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post,
            $"/api/review/cases/{caseId}/sync", token)).StatusCode);
        foreach (var path in new[] { "/api/projects", "/api/wallet/local-test-gas",
                     $"/api/projects/{scenario.ProjectId}/escrow/sync" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Post, path, token)).StatusCode);

        // Proving the wallet still requires the owner's actual signature.
        var walletKey = EthECKey.GenerateKey();
        var challenge = await ReadAsync(await SendAsync(client, HttpMethod.Post, "/api/wallet/challenge", token,
            new { WalletAddress = walletKey.GetPublicAddress() }));
        var message = challenge.GetProperty("message").GetString()!;
        var signer = new EthereumMessageSigner();
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(client, HttpMethod.Post, "/api/wallet/verify", token,
            new { Signature = signer.EncodeUTF8AndSign(message, EthECKey.GenerateKey()) })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Post, "/api/wallet/verify", token,
            new { Signature = signer.EncodeUTF8AndSign(message, walletKey) })).StatusCode);

        using var after = application.Services.CreateScope();
        var database = after.ServiceProvider.GetRequiredService<AppDbContext>();
        var userAfter = await database.Users.AsNoTracking().SingleAsync(item => item.Id == person.Id);
        Assert.False(userAfter.EmailConfirmed);
        Assert.False(userAfter.TwoFactorEnabled);
        Assert.NotNull(userAfter.WalletVerifiedAt);
        Assert.False(await database.RefreshTokens.AnyAsync(item => item.UserId == person.Id && item.AdminMfaVerifiedAt != null));
        var saved = await database.EscrowCases.AsNoTracking().SingleAsync(item => item.Id == caseId);
        Assert.Equal(reason, saved.ReviewNote);
        Assert.Null(saved.PaidToFreelancer);
        var escrow = await database.Escrows.AsNoTracking().SingleAsync(item => item.Id == saved.EscrowId);
        Assert.Equal(0m, escrow.ReleasedAmount);
        Assert.Equal(0m, escrow.PlatformFeesPaid);
        Assert.True(await database.AdminAccessLogs.AnyAsync(item => item.AdminId == person.Id && item.Action == "LocalAdminReviewEntered"));

        foreach (var header in new[] { "X-Forwarded-For", "Forwarded" })
        {
            using var forwarded = Request(HttpMethod.Get, "/api/admin/overview", token);
            forwarded.Headers.Add(header, "203.0.113.1");
            Assert.NotEqual(HttpStatusCode.OK, (await client.SendAsync(forwarded)).StatusCode);
        }
        using var hostileHost = Request(HttpMethod.Get, "/api/admin/overview", token);
        hostileHost.Headers.Host = "example.test";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(hostileHost)).StatusCode);

        await database.Escrows.Where(item => item.Id == escrow.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ChainId, 1L));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Put,
            notePath, token, new { Note = "This must not update a foreign-chain case." })).StatusCode);
        Assert.Equal(reason, (await database.EscrowCases.AsNoTracking().SingleAsync(item => item.Id == caseId)).ReviewNote);

        configuration["AdminSecurity:LocalReviewEnabled"] = "false";
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/overview", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin/overview", oldToken)).StatusCode);
        configuration["AdminSecurity:LocalReviewEnabled"] = "true";
        configuration["Blockchain:ChainId"] = "1";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(Entry, null)).StatusCode);
    }

    private static HttpClient Client(WebApplicationFactory<Program> app) => app.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    private sealed class LocalEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "TrustFlow.Api";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
