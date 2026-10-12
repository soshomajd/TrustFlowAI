using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustFlow.Api.Dtos.Freelancers;
using TrustFlow.Api.IntegrationTests.Infrastructure;

namespace TrustFlow.Api.IntegrationTests.Tests;

public sealed class FreelancerResumeTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task Resume_UploadReplaceDownloadAndDelete_UpdatesProfileMetadata()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        using var client = await CreateAccountAsync("Client");
        var first = BuildPdf("First resume");

        var upload = await UploadAsync(owner.Client, first, "resume.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var profile = await ReadProfileAsync(owner.Client, "me");
        Assert.True(profile.HasResume);
        Assert.Equal(first.Length, profile.ResumeFileSize);
        Assert.NotNull(profile.ResumeUploadedAt);

        var download = await client.Client.GetAsync($"/api/freelancers/{owner.Id}/resume");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(first, await download.Content.ReadAsByteArrayAsync());

        using var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/freelancers/{owner.Id}/resume")).StatusCode);

        var replacement = BuildPdf("Updated resume with different content");
        Assert.Equal(HttpStatusCode.OK,
            (await UploadAsync(owner.Client, replacement, "updated.PDF", "application/pdf")).StatusCode);
        Assert.Equal(replacement,
            await (await client.Client.GetAsync($"/api/freelancers/{owner.Id}/resume")).Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NoContent,
            (await owner.Client.DeleteAsync("/api/freelancers/me/resume")).StatusCode);
        profile = await ReadProfileAsync(owner.Client, "me");
        Assert.False(profile.HasResume);
        Assert.Null(profile.ResumeFileSize);
        Assert.Null(profile.ResumeUploadedAt);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.Client.GetAsync($"/api/freelancers/{owner.Id}/resume")).StatusCode);
    }

    [Theory]
    [InlineData("resume.exe", "application/pdf")]
    [InlineData("resume.pdf", "text/plain")]
    public async Task Resume_RejectsSpoofedExtensionOrContentType(string fileName, string contentType)
    {
        using var owner = await CreateAccountAsync("Freelancer");
        var response = await UploadAsync(owner.Client, BuildPdf("Resume"), fileName, contentType);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False((await ReadProfileAsync(owner.Client, "me")).HasResume);
    }

    [Fact]
    public async Task Resume_RejectsMalformedAndActivePdf()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        var malformed = await UploadAsync(owner.Client, "%PDF-not-valid"u8.ToArray(), "resume.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        var active = await UploadAsync(owner.Client, BuildPdf("Resume", active: true), "resume.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, active.StatusCode);
        Assert.Contains("scripts", await active.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resume_AllowsBenignOpenPageDestination()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        var response = await UploadAsync(owner.Client,
            BuildPdf("Resume", openDestination: true), "resume.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resume_ScanFailureOrThreat_DoesNotReplaceExistingResume()
    {
        using var owner = await CreateAccountAsync("Freelancer");
        var original = BuildPdf("Original");
        (await UploadAsync(owner.Client, original, "resume.pdf", "application/pdf")).EnsureSuccessStatusCode();

        var infected = BuildPdf("EICAR");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await UploadAsync(owner.Client, infected, "resume.pdf", "application/pdf")).StatusCode);
        var unavailable = BuildPdf("SCAN_UNAVAILABLE");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await UploadAsync(owner.Client, unavailable, "resume.pdf", "application/pdf")).StatusCode);

        Assert.Equal(original,
            await (await owner.Client.GetAsync($"/api/freelancers/{owner.Id}/resume")).Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Resume_UploadRequiresFreelancerRole()
    {
        using var client = await CreateAccountAsync("Client");
        Assert.Equal(HttpStatusCode.Forbidden,
            (await UploadAsync(client.Client, BuildPdf("Resume"), "resume.pdf", "application/pdf")).StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client, byte[] content, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/freelancers/me/resume", form);
    }

    private static byte[] BuildPdf(string text, bool active = false, bool openDestination = false)
    {
        var safeText = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var objects = new[]
        {
            $"<< /Type /Catalog /Pages 2 0 R{(active ? " /OpenAction << /S /JavaScript /JS (app.alert\\(1\\)) >>" : openDestination ? " /OpenAction [3 0 R /XYZ null null 0]" : "")} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            $"<< /Length {safeText.Length + 35} >>\nstream\nBT /F1 12 Tf 72 720 Td ({safeText}) Tj ET\nendstream"
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(builder.ToString()));
            builder.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(builder.ToString());
        builder.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) builder.Append(offset.ToString("D10")).Append(" 00000 n \n");
        builder.Append("trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false
    });

    private async Task<Account> CreateAccountAsync(string role)
    {
        var client = NewClient();
        var email = $"resume-{Guid.NewGuid():N}@trustflow.test";
        const string password = "Testpass1";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { FullName = $"Resume {role}", Email = email, Password = password, Role = role });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await TestAuth.ConfirmEmailAsync(client, email);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return new Account(registered.GetProperty("id").GetGuid(), client);
    }

    private static async Task<FreelancerProfileResponse> ReadProfileAsync(HttpClient client, string id)
    {
        var response = await client.GetAsync($"/api/freelancers/{id}/profile");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FreelancerProfileResponse>())!;
    }

    private sealed record Account(Guid Id, HttpClient Client) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
