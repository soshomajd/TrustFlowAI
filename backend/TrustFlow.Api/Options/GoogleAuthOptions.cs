namespace TrustFlow.Api.Options
{
    public class GoogleAuthOptions
    {
        public const string SectionName = "GoogleAuth";

        // The OAuth client ID is public by design: it is the audience that
        // Google ID tokens are checked against. Validating an ID token needs
        // no client secret, so none is stored here.
        public string ClientId { get; set; } = string.Empty;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ClientId);
    }
}
