using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Security;
using static TrustFlow.Api.IntegrationTests.Infrastructure.AiTestScenario;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class LocalAdminPreviewTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private const string Route = "/api/auth/admin/local-preview";

    [Fact]
    public async Task PreviewIsDisabledByDefault()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(Route, null)).StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public void FlagCannotEnablePreviewOutsideDevelopment(string environment)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["AdminSecurity:LocalPreviewEnabled"] = "true" }).Build();
        Assert.False(LocalAdminPreview.IsEnabled(new PreviewEnvironment(environment), settings));
        Assert.True(LocalAdminPreview.IsEnabled(new PreviewEnvironment("Development"), settings));
    }

    [Fact]
    public async Task LocalPreviewReadsRealAdminData_ButCannotWrite_OrChangeAccountVerification()
    {
        using var normal = factory.CreateClient();
        var person = await NewPersonAsync(normal, "Client", "Local preview owner");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(person.Id.ToString()))!;
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Admin)).Succeeded);
            user.EmailConfirmed = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
        using var enabled = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSecurity:LocalPreviewEnabled"] = "true",
                ["AdminSecurity:LocalPreviewEmail"] = person.Email,
                ["Review:AdminEmails:0"] = person.Email
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IWebHostEnvironment>();
                services.AddSingleton<IWebHostEnvironment>(new PreviewEnvironment("Development"));
            });
        });
        using var client = enabled.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        using var response = await client.PostAsync(Route, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var body = await ReadAsync(response);
        Assert.True(body.GetProperty("readOnly").GetBoolean());
        var token = body.GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin/overview", token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, HttpMethod.Get, "/api/admin/projects", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Post, $"/api/review/cases/{Guid.NewGuid()}/advice", token)).StatusCode);
        using var remote = Request(HttpMethod.Get, "/api/admin/overview", token);
        remote.Headers.Add("X-Test-Peer-IP", "203.0.113.20");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(remote)).StatusCode);
        using var hostileHost = Request(HttpMethod.Get, "/api/admin/overview", token);
        hostileHost.Headers.Host = "example.test";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(hostileHost)).StatusCode);
        using var scopeAfter = enabled.Services.CreateScope();
        var db = scopeAfter.ServiceProvider.GetRequiredService<AppDbContext>();
        var after = await db.Users.SingleAsync(item => item.Id == person.Id);
        Assert.False(after.EmailConfirmed);
        Assert.False(after.TwoFactorEnabled);
        Assert.False(await db.RefreshTokens.AnyAsync(item => item.UserId == person.Id && item.AdminMfaVerifiedAt != null));
        Assert.True(await db.AdminAccessLogs.AnyAsync(item => item.AdminId == person.Id && item.Action == "LocalAdminPreviewEntered"));
        var configuration = enabled.Services.GetRequiredService<IConfiguration>();
        configuration["AdminSecurity:LocalPreviewEnabled"] = "false";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(Route, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(client, HttpMethod.Get, "/api/admin/overview", token)).StatusCode);
    }

    private sealed class PreviewEnvironment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "TrustFlow.Api";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
