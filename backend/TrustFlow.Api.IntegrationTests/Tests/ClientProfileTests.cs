using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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
        Assert.False(initial.IdentityVerified);

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
        Assert.False(viewed.IdentityVerified);
        Assert.Equal(0, viewed.ProjectsPosted);
        Assert.Equal("NotSubmitted", viewed.VerificationStatus);

        var ownerBody = await client.Client.GetStringAsync("/api/clients/me/profile");
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

    [Fact]
    public async Task Verification_CompanySubmitAdminReviewAndApprove_PurgesSensitiveData()
    {
        using var client = await CreateAccountAsync("Client");
        using var freelancer = await CreateAccountAsync("Freelancer");
        using var admin = await CreateAdminAsync();
        var profileSave = await client.Client.PutAsJsonAsync("/api/clients/me/profile", new
        {
            AccountType = "Company", CompanyName = "Acme Studio", Overview = "Product team",
            Industry = "Design", Location = "Berlin", Website = "https://example.com", CompanySize = "2-10"
        });
        profileSave.EnsureSuccessStatusCode();

        var pdf = BuildPdf("Business registration");
        var submit = await SubmitVerificationAsync(client.Client, pdf, "Acme Studio GmbH", "DE", "HRB-12345");
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        Assert.Equal("Pending", (await ReadProfileAsync(client.Client, "me")).VerificationStatus);
        var publicProfile = await ReadProfileAsync(freelancer.Client, client.Id.ToString());
        Assert.False(publicProfile.IdentityVerified);
        Assert.Equal("NotSubmitted", publicProfile.VerificationStatus);

        var queue = await admin.Client.GetFromJsonAsync<ClientVerificationAdminResponse[]>(
            "/api/admin/client-verifications");
        var item = Assert.Single(queue!, value => value.ClientId == client.Id);
        Assert.Equal("Acme Studio GmbH", item.LegalName);
        Assert.Equal("HRB-12345", item.RegistrationNumber);
        Assert.Equal("DE", item.CountryCode);

        var document = await admin.Client.GetAsync($"/api/admin/client-verifications/{client.Id}/document");
        Assert.Equal(pdf, await document.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", document.Content.Headers.ContentDisposition?.DispositionType);

        var decision = await admin.Client.PostAsJsonAsync(
            $"/api/admin/client-verifications/{client.Id}/decision", new { Approve = true, Reason = "" });
        Assert.Equal(HttpStatusCode.NoContent, decision.StatusCode);
        var verified = await ReadProfileAsync(freelancer.Client, client.Id.ToString());
        Assert.True(verified.IdentityVerified);
        Assert.Equal("Verified", verified.VerificationStatus);
        Assert.NotNull(verified.IdentityVerifiedAt);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.Client.GetAsync($"/api/admin/client-verifications/{client.Id}/document")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ClientIdentityVerifications.AsNoTracking().SingleAsync(value => value.ClientId == client.Id);
        Assert.Null(stored.DocumentContentProtected);
        Assert.Null(stored.LegalNameProtected);
        Assert.Null(stored.RegistrationNumberProtected);
        Assert.NotEmpty(stored.DocumentSha256);
    }

    [Fact]
    public async Task Verification_RejectRequiresReason_AllowsResubmitAndReset()
    {
        using var client = await CreateAccountAsync("Client");
        using var admin = await CreateAdminAsync();
        var pdf = BuildPdf("Government identity document");
        var initialSubmit = await SubmitVerificationAsync(client.Client, pdf, "Jane Client", "CA", "");
        Assert.True(initialSubmit.IsSuccessStatusCode, await initialSubmit.Content.ReadAsStringAsync());

        var missingReason = await admin.Client.PostAsJsonAsync(
            $"/api/admin/client-verifications/{client.Id}/decision", new { Approve = false, Reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        var rejected = await admin.Client.PostAsJsonAsync(
            $"/api/admin/client-verifications/{client.Id}/decision",
            new { Approve = false, Reason = "Document image is not readable." });
        Assert.Equal(HttpStatusCode.NoContent, rejected.StatusCode);
        var owner = await ReadProfileAsync(client.Client, "me");
        Assert.Equal("Rejected", owner.VerificationStatus);
        Assert.Contains("not readable", owner.VerificationRejectionReason);

        Assert.Equal(HttpStatusCode.OK,
            (await SubmitVerificationAsync(client.Client, pdf, "Jane Client", "CA", "")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.Client.DeleteAsync("/api/clients/me/verification")).StatusCode);
        Assert.Equal("NotSubmitted", (await ReadProfileAsync(client.Client, "me")).VerificationStatus);
    }

    [Fact]
    public async Task Verification_RejectsMalformedThreatAndWrongRole()
    {
        using var client = await CreateAccountAsync("Client");
        using var freelancer = await CreateAccountAsync("Freelancer");
        var malformed = await SubmitVerificationAsync(client.Client, "%PDF-broken"u8.ToArray(), "Jane", "US", "");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        var infected = await SubmitVerificationAsync(client.Client, BuildPdf("EICAR"), "Jane", "US", "");
        Assert.Equal(HttpStatusCode.BadRequest, infected.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await SubmitVerificationAsync(freelancer.Client, BuildPdf("Document"), "Jane", "US", "")).StatusCode);
        Assert.Equal("NotSubmitted", (await ReadProfileAsync(client.Client, "me")).VerificationStatus);
    }

    private static async Task<HttpResponseMessage> SubmitVerificationAsync(
        HttpClient client, byte[] content, string legalName, string countryCode, string registrationNumber)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(legalName), "legalName");
        form.Add(new StringContent(countryCode), "countryCode");
        form.Add(new StringContent(registrationNumber), "registrationNumber");
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "identity.pdf");
        return await client.PostAsync("/api/clients/me/verification", form);
    }

    private static byte[] BuildPdf(string text)
    {
        var safeText = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
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

    private async Task<Account> CreateAdminAsync()
    {
        var account = await CreateAccountAsync("Client", login: false);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(account.Id.ToString());
            Assert.NotNull(user);
            var result = await userManager.AddToRoleAsync(user!, "Admin");
            Assert.True(result.Succeeded);
        }
        await LoginAsync(account.Client, account.Email);
        return account;
    }

    private async Task<Account> CreateAccountAsync(string role, bool login = true)
    {
        var client = NewClient();
        var email = $"client-profile-{Guid.NewGuid():N}@trustflow.test";
        const string password = "Testpass1";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new { FullName = $"Profile {role}", Email = email, Password = password, Role = role });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
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
