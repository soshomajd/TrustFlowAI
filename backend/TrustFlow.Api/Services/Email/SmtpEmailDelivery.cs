using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Email;

public sealed class SmtpEmailDelivery(
    IOptions<EmailOptions> options)
    : IEmailDelivery
{
    private const int TimeoutMilliseconds = 15_000;

    public async Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string textBody,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(
            settings.FromName,
            settings.FromAddress));

        message.To.Add(new MailboxAddress(
            toName,
            toAddress));

        message.Subject = subject;

        message.Body = new TextPart("plain")
        {
            Text = textBody
        };

        using var client = new SmtpClient
        {
            Timeout = TimeoutMilliseconds
        };

        // Auto picks implicit TLS on 465 and STARTTLS elsewhere. None is
        // only for a local mail catcher that has no TLS at all.
        await client.ConnectAsync(
            settings.Host,
            settings.Port,
            settings.UseStartTls
                ? SecureSocketOptions.Auto
                : SecureSocketOptions.None,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.User))
        {
            await client.AuthenticateAsync(
                settings.User,
                settings.Password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);

        await client.DisconnectAsync(true, cancellationToken);
    }
}
