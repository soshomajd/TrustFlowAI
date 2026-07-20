using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Services.Auth;

public interface IJwtTokenService
{
    JwtTokenResult CreateToken(
        ApplicationUser user,
        IEnumerable<string> roles
    );
}