using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Options;
using TrustFlow.Api.Services.Security;

namespace TrustFlow.Api.Services.Auth;

public sealed class JwtTokenService(
    IOptions<JwtSettings> options
) : IJwtTokenService
{
    private readonly JwtSettings _settings =
        options.Value;

    public JwtTokenResult CreateToken(
        ApplicationUser user,
        IEnumerable<string> roles, DateTimeOffset? adminMfaVerifiedAt = null)
        => BuildToken(user, roles, adminMfaVerifiedAt, false);

    public JwtTokenResult CreateLocalAdminPreviewToken(ApplicationUser user)
        => BuildToken(user, [Constants.AppRoles.Admin], null, true);

    private JwtTokenResult BuildToken(ApplicationUser user, IEnumerable<string> roles,
        DateTimeOffset? adminMfaVerifiedAt, bool localPreview)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(
            localPreview ? Math.Min(30, _settings.ExpirationMinutes) : _settings.ExpirationMinutes
        );

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()
            ),

            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()
            ),

            new(
                ClaimTypes.Name,
                user.FullName
            ),

            new(
                JwtRegisteredClaimNames.Email,
                user.Email ?? string.Empty
            ),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()
            )
        };

        var distinctRoles = roles
            .Where(role =>
                !string.IsNullOrWhiteSpace(role))
            .Distinct(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (var role in distinctRoles)
        {
            claims.Add(
                new Claim(
                    ClaimTypes.Role,
                    role
                )
            );
        }

        if (adminMfaVerifiedAt.HasValue)
        {
            claims.Add(new Claim(AdminSessionSecurity.MfaClaim, adminMfaVerifiedAt.Value.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            claims.Add(new Claim(AdminSessionSecurity.StampClaim, user.SecurityStamp ?? string.Empty));
            claims.Add(new Claim("amr", "mfa"));
        }

        if (localPreview)
        {
            claims.Add(new Claim(LocalAdminPreview.Claim, "true"));
            claims.Add(new Claim(AdminSessionSecurity.StampClaim, user.SecurityStamp ?? string.Empty));
        }

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    _settings.Key
                )
            );

        var signingCredentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256
            );

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials
        );

        var accessToken =
            new JwtSecurityTokenHandler()
                .WriteToken(token);

        return new JwtTokenResult(
            accessToken,
            expiresAtUtc
        );
    }
}
