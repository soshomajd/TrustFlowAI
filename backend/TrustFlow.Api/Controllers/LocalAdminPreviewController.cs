using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Models;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Auth;
using TrustFlow.Api.Services.Identity;
using TrustFlow.Api.Services.Security;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/auth/admin/local-preview")]
public sealed class LocalAdminPreviewController(IWebHostEnvironment environment, IConfiguration configuration,
    UserManager<ApplicationUser> users, IJwtTokenService tokens, AppDbContext db) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Enter(CancellationToken cancellationToken)
    {
        if (!LocalAdminPreview.IsEnabled(environment, configuration) || !LocalAdminPreview.IsLocal(HttpContext)) return NotFound();
        var email = configuration["AdminSecurity:LocalPreviewEmail"];
        if (string.IsNullOrWhiteSpace(email) || !ReviewerAccess.IsReviewer(configuration, email)) return NotFound();
        var user = await users.FindByEmailAsync(email);
        if (user is null || !await users.IsInRoleAsync(user, AppRoles.Admin)) return NotFound();
        var token = tokens.CreateLocalAdminPreviewToken(user);
        db.AdminAccessLogs.Add(new AdminAccessLog { AdminId = user.Id, Action = "LocalAdminPreviewEntered" });
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        // Deliberately no refresh cookie, account confirmation or MFA enrollment.
        return Ok(new { token.AccessToken, token.ExpiresAtUtc,
            User = new { user.Id, user.FullName, user.Email, Roles = new[] { AppRoles.Admin } } });
    }
}
