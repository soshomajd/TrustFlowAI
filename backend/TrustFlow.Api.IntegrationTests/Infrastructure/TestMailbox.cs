using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using TrustFlow.Api.Services.Email;

namespace TrustFlow.Api.IntegrationTests.Infrastructure;

public sealed record SentEmail(
    string To,
    string Subject,
    string Body);

// Stands in for the mail server. Every message is kept so a test can read
// the code the API mailed, which is the only way to complete the flow.
// Addresses are unique per test, so one shared store is safe.
public sealed class CapturingEmailDelivery : IEmailDelivery
{
    public Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string textBody,
        CancellationToken cancellationToken)
    {
        TestMailbox.Messages
            .GetOrAdd(
                toAddress.ToLowerInvariant(),
                _ => new ConcurrentQueue<SentEmail>())
            .Enqueue(new SentEmail(toAddress, subject, textBody));

        return Task.CompletedTask;
    }
}

public static class TestMailbox
{
    public static readonly ConcurrentDictionary<
        string,
        ConcurrentQueue<SentEmail>> Messages = new();

    public static IReadOnlyList<SentEmail> For(string email) =>
        Messages.TryGetValue(email.ToLowerInvariant(), out var queue)
            ? queue.ToArray()
            : [];

    public static string LatestCode(string email)
    {
        var latest = For(email).LastOrDefault()
            ?? throw new InvalidOperationException(
                $"No email was sent to {email}.");

        var match = Regex.Match(
            latest.Body,
            @"^\s*(\d{6})\s*$", RegexOptions.Multiline);

        return match.Success
            ? match.Groups[1].Value
            : throw new InvalidOperationException(
                "The latest email holds no 6 digit code.");
    }
}

public static class TestAuth
{
    // Registering leaves an account that cannot sign in. Tests that only
    // need a working user call this straight after registering.
    public static async Task ConfirmEmailAsync(
        HttpClient client,
        string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/verify-email",
            new
            {
                Email = email,
                Code = TestMailbox.LatestCode(email)
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
