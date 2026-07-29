using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Services.Auth;

public sealed record RefreshTokenCreationResult(
    string RawToken,
    RefreshToken RefreshToken
);