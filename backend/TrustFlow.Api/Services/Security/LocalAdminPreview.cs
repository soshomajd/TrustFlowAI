using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Identity;

namespace TrustFlow.Api.Services.Security;

// Temporary local inspection only. Normal password/email/MFA code remains intact.
// Disable AdminSecurity:LocalPreviewEnabled to restore normal entry locally.
public static class LocalAdminPreview
{
    public const string Claim = "tf_local_admin_preview";

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration)
        => environment.IsDevelopment() && configuration.GetValue<bool>("AdminSecurity:LocalPreviewEnabled");

    public static bool IsLocal(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer?.IsIPv4MappedToIPv6 == true) peer = peer.MapToIPv4();
        return peer is not null && IPAddress.IsLoopback(peer)
            && context.Request.Host.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1"
            && !context.Request.Headers.ContainsKey("Forwarded")
            && !context.Request.Headers.ContainsKey("X-Forwarded-For");
    }

    public static async Task<bool> ValidateAsync(HttpContext context, ClaimsPrincipal principal,
        UserManager<ApplicationUser> users, IHostEnvironment environment, IConfiguration configuration)
    {
        if (!IsEnabled(environment, configuration) || !IsLocal(context)
            || !HttpMethods.IsGet(context.Request.Method)
            || !principal.IsInRole(AppRoles.Admin)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var user = await users.FindByIdAsync(id.ToString());
        return user is not null && ReviewerAccess.IsReviewer(configuration, user.Email ?? "")
            && string.Equals(user.Email, configuration["AdminSecurity:LocalPreviewEmail"], StringComparison.OrdinalIgnoreCase)
            && string.Equals(user.SecurityStamp, principal.FindFirstValue(AdminSessionSecurity.StampClaim), StringComparison.Ordinal)
            && await users.IsInRoleAsync(user, AppRoles.Admin);
    }
}
