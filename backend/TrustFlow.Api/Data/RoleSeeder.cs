using Microsoft.AspNetCore.Identity;
using TrustFlow.Api.Constants;

namespace TrustFlow.Api.Data;

public static class RoleSeeder
{
    public static async Task SeedRolesAsync(
        this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<
                RoleManager<IdentityRole<Guid>>
            >();

        string[] roles =
        [
            AppRoles.Client,
            AppRoles.Freelancer,
            AppRoles.Admin
        ];

        foreach (var roleName in roles)
        {
            var roleExists =
                await roleManager.RoleExistsAsync(roleName);

            if (roleExists)
            {
                continue;
            }

            var role = new IdentityRole<Guid>
            {
                Name = roleName
            };

            var result =
                await roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    ", ",
                    result.Errors.Select(
                        error => error.Description
                    )
                );

                throw new InvalidOperationException(
                    $"Could not create role '{roleName}': {errors}"
                );
            }
        }
    }
}