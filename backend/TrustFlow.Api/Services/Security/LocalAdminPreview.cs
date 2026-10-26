using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Identity;

namespace TrustFlow.Api.Services.Security;

// Opt-in local development access. Normal password/email/MFA remains intact.
// Disable AdminSecurity:LocalPreviewEnabled to restore normal entry locally.
public static class LocalAdminPreview
{
    public const string Claim = "tf_local_admin_preview";
    public const string ReviewClaim = "tf_local_admin_review";

    public static bool IsEnabled(IHostEnvironment environment, IConfiguration configuration)
        => environment.IsDevelopment() && configuration.GetValue<bool>("AdminSecurity:LocalPreviewEnabled");

    public static bool ReviewEnabled(IHostEnvironment environment, IConfiguration configuration)
        => IsEnabled(environment, configuration)
            && configuration.GetValue<bool>("AdminSecurity:LocalReviewEnabled")
            && configuration.GetValue<long>("Blockchain:ChainId") == 31_337
            && Uri.TryCreate(configuration["Blockchain:RpcUrl"], UriKind.Absolute, out var rpc)
            && rpc.IsLoopback && rpc.UserInfo.Length == 0
            && rpc.Scheme is "http" or "https";

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
            || !principal.IsInRole(AppRoles.Admin)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var review = principal.HasClaim(ReviewClaim, "true");
        if (review && !ReviewEnabled(environment, configuration)) return false;
        if (!HttpMethods.IsGet(context.Request.Method)
            && (!review || !await ReviewWriteAllowedAsync(context))) return false;
        var user = await users.FindByIdAsync(id.ToString());
        return user is not null && ReviewerAccess.IsReviewer(configuration, user.Email ?? "")
            && string.Equals(user.Email, configuration["AdminSecurity:LocalPreviewEmail"], StringComparison.OrdinalIgnoreCase)
            && string.Equals(user.SecurityStamp, principal.FindFirstValue(AdminSessionSecurity.StampClaim), StringComparison.Ordinal)
            && await users.IsInRoleAsync(user, AppRoles.Admin);
    }

    private static async Task<bool> ReviewWriteAllowedAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
        if (HttpMethods.IsPost(context.Request.Method)
            && path is "/api/wallet/challenge" or "/api/wallet/verify") return true;

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || parts[0] != "api" || parts[1] != "review" || parts[2] != "cases"
            || !Guid.TryParse(parts[3], out var caseId)
            || !(HttpMethods.IsPut(context.Request.Method) && parts[4] == "note"
                || HttpMethods.IsPost(context.Request.Method) && parts[4] is "sync" or "advice")) return false;

        // This capability is for local test escrows only. Every other write
        // (including account changes, refresh, marketplace and funding) is refused.
        return await context.RequestServices.GetRequiredService<AppDbContext>().EscrowCases
            .AsNoTracking().AnyAsync(item => item.Id == caseId && item.Escrow.ChainId == 31_337,
                context.RequestAborted);
    }
}
