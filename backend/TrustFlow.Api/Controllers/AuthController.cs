using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Dtos.Auth;
using TrustFlow.Api.Models.Identity;
using TrustFlow.Api.Services.Auth;

namespace TrustFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    SignInManager<ApplicationUser> signInManager,
    IJwtTokenService jwtTokenService)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var role = request.Role.Trim();

        if (role != AppRoles.Client &&
            role != AppRoles.Freelancer)
        {
            return BadRequest(new
            {
                message = "Role must be Client or Freelancer."
            });
        }

        var existingUser = await userManager.FindByEmailAsync(
            request.Email
        );

        if (existingUser is not null)
        {
            return Conflict(new
            {
                message = "A user with this email already exists."
            });
        }

        var roleExists = await roleManager.RoleExistsAsync(role);

        if (!roleExists)
        {
            var createRoleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>
                {
                    Name = role
                }
            );

            if (!createRoleResult.Succeeded)
            {
                return BadRequest(new
                {
                    errors = createRoleResult.Errors
                        .Select(error => error.Description)
                });
            }
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            UserName = request.Email.Trim()
        };

        var createUserResult = await userManager.CreateAsync(
            user,
            request.Password
        );

        if (!createUserResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = createUserResult.Errors
                    .Select(error => error.Description)
            });
        }

        var addRoleResult = await userManager.AddToRoleAsync(
            user,
            role
        );

        if (!addRoleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);

            return BadRequest(new
            {
                errors = addRoleResult.Errors
                    .Select(error => error.Description)
            });
        }

        return Created($"/api/users/{user.Id}", new
        {
            user.Id,
            user.FullName,
            user.Email,
            Role = role,
            user.CreatedAt
        });
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(
    LoginRequest request)
    {
        var email = request.Email.Trim();

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
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
                message = "Invalid email or password."
            });
        }

        var roles = await userManager.GetRolesAsync(user);

        var tokenResult = jwtTokenService.CreateToken(
            user,
            roles
        );

        return Ok(new
        {
            tokenResult.AccessToken,
            tokenResult.ExpiresAtUtc,

            User = new
            {
                user.Id,
                user.FullName,
                user.Email,
                Roles = roles
            }
        });
    }
}