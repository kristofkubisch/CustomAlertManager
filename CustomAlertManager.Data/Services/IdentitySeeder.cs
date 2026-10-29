using CustomAlertManager.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace CustomAlertManager.Data.Services;

public sealed record AdminSeedOptions(string Username, string Email, string Password);

public static class IdentitySeeder
{
    public const string UserRole = "User";
    public const string AdminRole = "Admin";

    // Creates the roles, and the admin account when admin options are configured.
    // An existing account with the same username or email only gets the Admin role added.
    public static async Task SeedAsync(
        RoleManager<IdentityRole<int>> roleManager,
        UserManager<ApplicationUser> userManager,
        AdminSeedOptions? admin)
    {
        foreach (var role in new[] { UserRole, AdminRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<int>(role));
            }
        }

        if (admin is null
            || string.IsNullOrWhiteSpace(admin.Username)
            || string.IsNullOrWhiteSpace(admin.Email)
            || string.IsNullOrWhiteSpace(admin.Password))
        {
            return;
        }

        var user = await userManager.FindByNameAsync(admin.Username)
            ?? await userManager.FindByEmailAsync(admin.Email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = admin.Username, Email = admin.Email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(user, admin.Password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    "Could not create the seeded admin account: " + string.Join("; ", created.Errors.Select(error => error.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, AdminRole))
        {
            await userManager.AddToRoleAsync(user, AdminRole);
        }
    }
}
