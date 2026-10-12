using Microsoft.AspNetCore.Identity;
using TrustFlow.Api.Constants;
using TrustFlow.Api.Models.Identity;

namespace TrustFlow.Api.Services.Identity;

// Who may sit at the reviewer's desk. The list lives in configuration
// (Review:AdminEmails), never in the database or the browser, so nobody can
// grant it to themselves. Registration still sends a code to the address, so
// holding the role needs control of the mailbox as well as the setting.
public static class ReviewerAccess
{
    public const string SectionName = "Review:AdminEmails";

    public static IReadOnlyList<string> Emails(
        IConfiguration configuration) =>
        (configuration.GetSection(SectionName).Get<string[]>() ?? [])
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

    public static bool IsReviewer(
        IConfiguration configuration,
        string email) =>
        Emails(configuration).Any(item =>
            string.Equals(
                item,
                email.Trim(),
                StringComparison.OrdinalIgnoreCase));

    // Called at startup for accounts that already exist.
    public static async Task SeedAsync(WebApplication app)
    {
        var emails = Emails(app.Configuration);

        if (emails.Count == 0)
        {
            return;
        }

        using var scope = app.Services.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user is null ||
                await userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                continue;
            }

            var result = await userManager.AddToRoleAsync(
                user,
                AppRoles.Admin);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not give the reviewer role to {email}.");
            }
        }
    }
}
