namespace TrustFlow.Api.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 587;

    // STARTTLS on the submission port. Set false only for a local catcher
    // such as Mailpit that does not speak TLS.
    public bool UseStartTls { get; init; } = true;

    // The password comes from an environment variable or user secrets,
    // never from a committed settings file.
    public string User { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string FromAddress { get; init; } = string.Empty;

    public string FromName { get; init; } = "TrustFlow";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(FromAddress);
}
