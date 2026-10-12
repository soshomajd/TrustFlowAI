namespace TrustFlow.Api.Services.Email;

public interface IEmailDelivery
{
    Task SendAsync(
        string toAddress,
        string toName,
        string subject,
        string textBody,
        CancellationToken cancellationToken);
}
