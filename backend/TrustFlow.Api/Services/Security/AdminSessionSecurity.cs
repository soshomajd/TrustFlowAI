using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Services.Security;

public static class AdminSessionSecurity
{
    public const string MfaClaim = "tf_admin_mfa";
    public const string StampClaim = "tf_security_stamp";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    public static bool IsFresh(DateTimeOffset? at) => at.HasValue
        && at <= DateTimeOffset.UtcNow.AddMinutes(1) && DateTimeOffset.UtcNow - at <= Lifetime;

    public static async Task<bool> ValidateAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        if (!principal.IsInRole(AppRoles.Admin)) return true;
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            || !long.TryParse(principal.FindFirstValue(MfaClaim), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds < 0 || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return false;
        if (!IsFresh(DateTimeOffset.FromUnixTimeSeconds(seconds))) return false;
        var user = await users.FindByIdAsync(id.ToString());
        return user is { EmailConfirmed: true, TwoFactorEnabled: true }
            && string.Equals(user.SecurityStamp, principal.FindFirstValue(StampClaim), StringComparison.Ordinal)
            && !await users.IsLockedOutAsync(user) && await users.IsInRoleAsync(user, AppRoles.Admin);
    }
}
