using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Data;
using TrustFlow.Api.Dtos.Auth;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Auth;
using Microsoft.AspNetCore.RateLimiting;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService,
    IRefreshTokenService refreshTokenService,
    AppDbContext dbContext,
    IWebHostEnvironment environment)
    : ControllerBase
{
    private const string RefreshTokenCookieName =
        "trustflow_refresh_token";

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Register)]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request)
    {
        var role = request.Role.Trim();

        if (role != AppRoles.Client &&
            role != AppRoles.Freelancer)
        {
            return BadRequest(new
            {
                message =
                    "Role must be Client or Freelancer."
            });
        }

        var email = request.Email.Trim();

        var existingUser =
            await userManager.FindByEmailAsync(email);

        if (existingUser is not null)
        {
            return Conflict(new
            {
                message =
                    "A user with this email already exists."
            });
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            UserName = email
        };

        var createUserResult =
            await userManager.CreateAsync(
                user,
                request.Password
            );

        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = createUserResult.Errors
                    .Select(error =>
                        error.Description)
            });
        }

        var addRoleResult =
            await userManager.AddToRoleAsync(
                user,
                role
            );

        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return BadRequest(new
            {
                errors = addRoleResult.Errors
                    .Select(error =>
                        error.Description)
            });
        }

        return Created(
            $"/api/users/{user.Id}",
            new
            {
                user.Id,
                user.FullName,
                user.Email,
                Role = role,
                user.CreatedAt
            }
        );
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        var user =
            await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email or password."
            });
        }

        var passwordResult =
            await signInManager.CheckPasswordSignInAsync(
                user,
                request.Password,
                lockoutOnFailure: true
            );

        if (!passwordResult.Succeeded)
        {
            return Unauthorized(new
            {
                message =
                    "Invalid email or password."
            });
        }

        var roles =
            await userManager.GetRolesAsync(user);

        var accessTokenResult =
            jwtTokenService.CreateToken(
                user,
                roles
            );

        var refreshTokenResult =
            refreshTokenService.Create(user.Id);

        dbContext.RefreshTokens.Add(
            refreshTokenResult.RefreshToken
        );

        await dbContext.SaveChangesAsync(
            cancellationToken
        );

        AppendRefreshTokenCookie(
            refreshTokenResult.RawToken,
            refreshTokenResult.RefreshToken.ExpiresAt
        );

        return Ok(new
        {
            accessTokenResult.AccessToken,
            accessTokenResult.ExpiresAtUtc,

            User = new
            {
                user.Id,
                user.FullName,
                user.Email,
                Roles = roles
            }
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting(
    RateLimitPolicies.RefreshToken)]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAccessToken(
        CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var rawToken) ||
            string.IsNullOrWhiteSpace(rawToken))
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "Refresh token is missing."
            });
        }

        var tokenHash =
            refreshTokenService.HashToken(rawToken);

        var storedToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(refreshToken =>
                refreshToken.TokenHash == tokenHash)
            .Select(refreshToken => new
            {
                refreshToken.Id,
                refreshToken.UserId,
                refreshToken.FamilyId,
                refreshToken.ExpiresAt,
                refreshToken.RevokedAt,
                refreshToken.ReplacedByTokenHash
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (storedToken is null)
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "Refresh token is invalid."
            });
        }

        var now = DateTimeOffset.UtcNow;

        if (storedToken.RevokedAt.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(
                    storedToken.ReplacedByTokenHash))
            {
                await RevokeTokenFamilyAsync(
                    storedToken.UserId,
                    storedToken.FamilyId,
                    now,
                    cancellationToken
                );
            }

            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "Refresh token has already been used or revoked."
            });
        }

        if (storedToken.ExpiresAt <= now)
        {
            await dbContext.RefreshTokens
                .Where(refreshToken =>
                    refreshToken.Id == storedToken.Id &&
                    refreshToken.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken =>
                            refreshToken.RevokedAt,
                        (DateTimeOffset?)now
                    ),
                    cancellationToken
                );

            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "Refresh token has expired."
            });
        }

        var user = await userManager.FindByIdAsync(
            storedToken.UserId.ToString()
        );

        if (user is null)
        {
            await RevokeTokenFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                cancellationToken
            );

            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "User no longer exists."
            });
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            await RevokeTokenFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                cancellationToken
            );

            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "User account is locked."
            });
        }

        var roles =
            await userManager.GetRolesAsync(user);

        var newRefreshTokenResult =
            refreshTokenService.Create(
                user.Id,
                storedToken.FamilyId
            );

        await using var transaction =
            await dbContext.Database
                .BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken
                );

        var claimedRows = await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.Id == storedToken.Id &&
                refreshToken.RevokedAt == null &&
                refreshToken.ExpiresAt > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        refreshToken =>
                            refreshToken.RevokedAt,
                        (DateTimeOffset?)now
                    )
                    .SetProperty(
                        refreshToken =>
                            refreshToken.ReplacedByTokenHash,
                        newRefreshTokenResult
                            .RefreshToken
                            .TokenHash
                    ),
                cancellationToken
            );

        if (claimedRows != 1)
        {
            await transaction.RollbackAsync(
                cancellationToken
            );

            await RevokeTokenFamilyAsync(
                storedToken.UserId,
                storedToken.FamilyId,
                now,
                cancellationToken
            );

            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message =
                    "Refresh token reuse was detected."
            });
        }

        dbContext.RefreshTokens.Add(
            newRefreshTokenResult.RefreshToken
        );

        await dbContext.SaveChangesAsync(
            cancellationToken
        );

        await transaction.CommitAsync(
            cancellationToken
        );

        var accessTokenResult =
            jwtTokenService.CreateToken(
                user,
                roles
            );

        AppendRefreshTokenCookie(
            newRefreshTokenResult.RawToken,
            newRefreshTokenResult
                .RefreshToken
                .ExpiresAt
        );

        return Ok(new
        {
            accessTokenResult.AccessToken,
            accessTokenResult.ExpiresAtUtc,

            User = new
            {
                user.Id,
                user.FullName,
                user.Email,
                Roles = roles
            }
        });
    }
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
    CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(
                RefreshTokenCookieName,
                out var rawToken) &&
            !string.IsNullOrWhiteSpace(rawToken))
        {
            var tokenHash =
                refreshTokenService.HashToken(rawToken);

            var now = DateTimeOffset.UtcNow;

            await dbContext.RefreshTokens
                .Where(refreshToken =>
                    refreshToken.TokenHash == tokenHash &&
                    refreshToken.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        refreshToken =>
                            refreshToken.RevokedAt,
                        (DateTimeOffset?)now
                    ),
                    cancellationToken
                );
        }

        DeleteRefreshTokenCookie();

        return NoContent();
    }
    [Authorize]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(
    CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
                userIdValue,
                out var userId))
        {
            DeleteRefreshTokenCookie();

            return Unauthorized(new
            {
                message = "Invalid user identity."
            });
        }

        var now = DateTimeOffset.UtcNow;

        await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.UserId == userId &&
                refreshToken.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    refreshToken =>
                        refreshToken.RevokedAt,
                    (DateTimeOffset?)now
                ),
                cancellationToken
            );

        DeleteRefreshTokenCookie();

        return NoContent();
    }


    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdValue = User.FindFirstValue(
            ClaimTypes.NameIdentifier
        );

        if (!Guid.TryParse(
                userIdValue,
                out var userId))
        {
            return Unauthorized(new
            {
                message =
                    "Invalid user identity."
            });
        }

        var user = await userManager.FindByIdAsync(
            userId.ToString()
        );

        if (user is null)
        {
            return Unauthorized(new
            {
                message =
                    "User no longer exists."
            });
        }

        var roles =
            await userManager.GetRolesAsync(user);

        return Ok(new
        {
            user.Id,
            user.FullName,
            user.Email,
            Roles = roles,
            user.CreatedAt
        });
    }

    private async Task RevokeTokenFamilyAsync(
        Guid userId,
        Guid familyId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.UserId == userId &&
                refreshToken.FamilyId == familyId &&
                refreshToken.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    refreshToken =>
                        refreshToken.RevokedAt,
                    (DateTimeOffset?)revokedAt
                ),
                cancellationToken
            );
    }

    private void AppendRefreshTokenCookie(
        string rawToken,
        DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(
            RefreshTokenCookieName,
            rawToken,
            new CookieOptions
            {
                HttpOnly = true,

                Secure =
                    !environment.IsDevelopment() ||
                    Request.IsHttps,

                SameSite = SameSiteMode.Lax,

                Expires = expiresAt,

                IsEssential = true,

                Path = "/api/auth"
            }
        );
    }

    private void DeleteRefreshTokenCookie()
    {
        Response.Cookies.Delete(
            RefreshTokenCookieName,
            new CookieOptions
            {
                Secure =
                    !environment.IsDevelopment() ||
                    Request.IsHttps,

                SameSite = SameSiteMode.Lax,

                Path = "/api/auth"
            }
        );
    }
}