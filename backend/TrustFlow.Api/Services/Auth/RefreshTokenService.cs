using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;

namespace TrustFlow.Api.Services.Auth;

public sealed class RefreshTokenService(
    IOptions<JwtSettings> jwtOptions
) : IRefreshTokenService
{
    private readonly JwtSettings _jwtSettings =
        jwtOptions.Value;

    public RefreshTokenCreationResult Create(
        Guid userId,
        Guid? familyId = null)
    {
        var rawToken = GenerateSecureToken();

        var now = DateTimeOffset.UtcNow;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),

            UserId = userId,

            TokenHash = HashToken(rawToken),

            FamilyId = familyId ?? Guid.NewGuid(),

            CreatedAt = now,

            ExpiresAt = now.AddDays(
                _jwtSettings.RefreshTokenExpirationDays
            )
        };

        return new RefreshTokenCreationResult(
            rawToken,
            refreshToken
        );
    }

    public string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rawToken
        );

        var tokenBytes = Encoding.UTF8.GetBytes(
            rawToken
        );

        var hashBytes = SHA256.HashData(
            tokenBytes
        );

        return Convert.ToHexString(hashBytes);
    }

    private static string GenerateSecureToken()
    {
        var randomBytes =
            RandomNumberGenerator.GetBytes(64);

        return WebEncoders.Base64UrlEncode(
            randomBytes
        );
    }
}