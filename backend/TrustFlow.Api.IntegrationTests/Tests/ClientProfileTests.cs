using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using SkiaSharp;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Clients;
using TrustFlow.Api.IntegrationTests.Infrastructure;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class ClientProfileTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Profile_SavesIndividualAndCompany_WithoutWritableTrustFields()
    {
        using var client = await CreateAccountAsync("Client");
        using var freelancer = await CreateAccountAsync("Freelancer");
        var initial = await ReadProfileAsync(client.Client, "me");
        Assert.Equal("Individual", initial.AccountType);
        Assert.Equal(client.Id, initial.UserId);

        var update = new
        {
            AccountType = "Company", CompanyName = "  Northstar Labs  ",
            Overview = "We hire small product teams.", Industry = "Software",
            Location = "Toronto, Canada", Website = "https://example.com",
            CompanySize = "11-50", IdentityVerified = true, ProjectsPosted = 999
        };
        var save = await client.Client.PutAsJsonAsync("/api/clients/me/profile", update);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var viewed = await ReadProfileAsync(freelancer.Client, client.Id.ToString());
        Assert.Equal("Company", viewed.AccountType);
        Assert.Equal("Northstar Labs", viewed.DisplayName);
        Assert.Equal("Northstar Labs", viewed.CompanyName);
        Assert.Equal("Software", viewed.Industry);
        Assert.Equal(0, viewed.ProjectsPosted);

        var ownerBody = await client.Client.GetStringAsync("/api/clients/me/profile");
        Assert.DoesNotContain("identityVerified", ownerBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("legalName", ownerBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("registrationNumber", ownerBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("documentSha", ownerBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_RequiresAuthenticationAndClientRole()
    {
        using var client = await CreateAccountAsync("Client");
        using var freelancer = await CreateAccountAsync("Freelancer");
        using var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/clients/{client.Id}/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await freelancer.Client.GetAsync("/api/clients/me/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await freelancer.Client.PutAsJsonAsync("/api/clients/me/profile", new { AccountType = "Individual" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.Client.GetAsync($"/api/clients/{freelancer.Id}/profile")).StatusCode);
    }


    [Theory]
    [InlineData("Client")]
    [InlineData("Freelancer")]
    public async Task Photo_UploadReplaceReadAndDelete_AreBoundToOwner(string role)
    {
        using var owner = await CreateAccountAsync(role);
        using var other = await CreateAccountAsync("Client");
        using var anonymous = NewClient();
        var original = MakePhoto();
        var upload = await UploadPhoto(owner.Client, original, x: "100");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var saved = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("image").GetString()!;
        var bytes = Convert.FromBase64String(saved.Split(',')[1]);
        using (var decoded = SKBitmap.Decode(bytes))
        {
            Assert.Equal(512, decoded.Width);
            Assert.Equal(512, decoded.Height);
            Assert.True(decoded.GetPixel(256, 256).Blue > 240); // Right-hand crop selected.
        }
        Assert.NotEqual(original, bytes);
        Assert.Equal(saved, (await other.Client.GetFromJsonAsync<JsonElement>($"/api/users/{owner.Id}/photo")).GetProperty("image").GetString());
        Assert.Equal(JsonValueKind.Null, (await other.Client.GetFromJsonAsync<JsonElement>($"/api/users/{other.Id}/photo")).GetProperty("image").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/users/{owner.Id}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadPhoto(anonymous, original)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await other.Client.DeleteAsync($"/api/users/{owner.Id}/photo")).StatusCode);
        var replacement = await UploadPhoto(owner.Client, original, x: "0");
        replacement.EnsureSuccessStatusCode();
        Assert.NotEqual(saved, (await replacement.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("image").GetString());
        (await other.Client.DeleteAsync("/api/users/me/photo")).EnsureSuccessStatusCode();
        Assert.NotEqual(JsonValueKind.Null, (await owner.Client.GetFromJsonAsync<JsonElement>($"/api/users/{owner.Id}/photo")).GetProperty("image").ValueKind);
        (await owner.Client.DeleteAsync("/api/users/me/photo")).EnsureSuccessStatusCode();
        Assert.Equal(JsonValueKind.Null, (await owner.Client.GetFromJsonAsync<JsonElement>($"/api/users/{owner.Id}/photo")).GetProperty("image").ValueKind);
    }

    [Fact]
    public async Task Photo_RejectsUnsafeInputs_AndPreservesPreviousPhoto()
    {
        using var owner = await CreateAccountAsync("Client");
        var photo = MakePhoto();
        var savedResponse = await UploadPhoto(owner.Client, photo);
        savedResponse.EnsureSuccessStatusCode();
        var saved = await savedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>"), "face.svg", "image/svg+xml")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, photo, "face.jpg", "image/jpeg")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, photo[..40])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, MakePhoto(64, 64))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, MakePhoto(8001, 128))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, photo, zoom: "0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, photo, x: "101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await UploadPhoto(owner.Client, [.. photo, .. Encoding.ASCII.GetBytes("EICAR")])).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await UploadPhoto(owner.Client, [.. photo, .. Encoding.ASCII.GetBytes("SCAN_UNAVAILABLE")])).StatusCode);
        Assert.Equal(saved, await owner.Client.GetStringAsync($"/api/users/{owner.Id}/photo"));
        var oversized = await UploadPhoto(owner.Client, new byte[5 * 1024 * 1024 + 1]);
        Assert.Contains(oversized.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
    }

    [Fact]
    public async Task IdentityVerification_RemovedFromRoutesAndProfile()
    {
        using var owner = await CreateAccountAsync("Client");
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.PostAsJsonAsync("/api/clients/me/verification", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync("/api/admin/client-verifications")).StatusCode);
        var body = await owner.Client.GetStringAsync("/api/clients/me/profile");
        Assert.DoesNotContain("identityVerified", body);
        Assert.DoesNotContain("verificationStatus", body);
    }

    private static byte[] MakePhoto(int width = 600, int height = 300)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        surface.Canvas.DrawRect(new SKRect(width / 2, 0, width, height), paint);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<HttpResponseMessage> UploadPhoto(HttpClient client, byte[] bytes,
        string fileName = "face.png", string contentType = "image/png", string zoom = "1", string x = "50")
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        form.Add(new StringContent(zoom), "zoom");
        form.Add(new StringContent(x), "x");
        return await client.PostAsync("/api/users/me/photo", form);
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });

    private async Task<Account> CreateAccountAsync(string role, bool login = true)
    {
        var client = NewClient();
        var email = $"client-profile-{Guid.NewGuid():N}@trustflow.test";
        const string password = "Testpass1";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { FullName = $"Profile {role}", Email = email, Password = password, Role = role });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await TestAuth.ConfirmEmailAsync(client, email);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        if (login) await LoginAsync(client, email);
        return new Account(registered.GetProperty("id").GetGuid(), email, client);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Testpass1" });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
    }

    private static async Task<ClientProfileResponse> ReadProfileAsync(HttpClient client, string id)
    {
        var response = await client.GetAsync($"/api/clients/{id}/profile");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ClientProfileResponse>())!;
    }

    private sealed record Account(Guid Id, string Email, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
