using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public static class TestAdminSignIn
{
    public static async Task<HttpResponseMessage> CompleteAsync(HttpClient client, IServiceProvider services, string email, string password)
    {
        var initial = await client.PostAsJsonAsync("/api/auth/admin/login", new { Email = email, Password = password });
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        var prompt = await initial.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(prompt.GetProperty("mfaRequired").GetBoolean());
        var result = await client.PostAsJsonAsync("/api/auth/admin/mfa", new
        {
            ChallengeToken = prompt.GetProperty("challengeToken").GetString(), Code = TestMailbox.LatestCode(email)
        });
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        return result;
    }

}
