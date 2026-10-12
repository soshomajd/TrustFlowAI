namespace TrustFlow.Api.Options;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public const string MockProvider = "Mock";

    public const string AnthropicProvider = "Anthropic";

    // The whole feature can be switched off without a deploy.
    public bool Enabled { get; init; } = true;

    // Mock answers with fixed, clearly labelled demo output and costs
    // nothing. Anthropic calls the Messages API and needs ApiKey.
    public string Provider { get; init; } = MockProvider;

    // Comes from an environment variable or user secrets, never from a
    // committed settings file.
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } =
        "https://api.anthropic.com";

    public string Model { get; init; } = "claude-sonnet-5-5";

    // A stronger model for rulings. Empty means the same as Model.
    public string DisputeModel { get; init; } = string.Empty;

    public int MaxOutputTokens { get; init; } = 4096;

    public int TimeoutSeconds { get; init; } = 90;

    // Null leaves the parameter out, which is the safe choice because not
    // every model accepts it.
    public double? Temperature { get; init; }

    // Runs per user in a rolling 24 hours, across every feature. This is
    // the brake on a bill nobody meant to run up.
    public int DailyRunsPerUser { get; init; } = 40;

    // How long the other side gets to write their statement before either
    // party may ask for a ruling without it.
    public int DisputeResponseWindowHours { get; init; } = 48;

    // Below this the model's verdict is not used and the dispute is
    // recorded as inconclusive.
    public double MinimumRulingConfidence { get; init; } = 0.6;

    public int MaxRulingAttempts { get; init; } = 3;

    public bool IsMock =>
        string.Equals(Provider, MockProvider,
            StringComparison.OrdinalIgnoreCase);

    public string EffectiveDisputeModel =>
        string.IsNullOrWhiteSpace(DisputeModel) ? Model : DisputeModel;
}
