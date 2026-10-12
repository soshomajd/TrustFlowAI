namespace TrustFlow.Api.Services.Email;

// Development fallback for when no SMTP server is configured. It prints the
// message, including the code, to the API log so the flow can be walked
// through without a mailbox. It is never registered outside Development.
public sealed class LogEmailDelivery(
    ILogger<LogEmailDelivery> logger)
    : IEmailDelivery
{
    public Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string textBody,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "No SMTP server is configured, so this email was not sent. " +
            "To: {ToAddress}. Subject: {Subject}.{NewLine}{Body}",
            toAddress,
            subject,
            Environment.NewLine,
            textBody);

        return Task.CompletedTask;
    }
}
