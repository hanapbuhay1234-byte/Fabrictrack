using FabriTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace FabriTrack.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var role in new[] { Roles.Admin, Roles.Staff })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                        throw new InvalidOperationException($"Could not create the '{role}' role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
                }
            }

            var config = services.GetRequiredService<IConfiguration>();

            // Read seeded admin credentials from configuration (user-secrets / env).
            // Do NOT store real credentials in source control. If config values are
            // not provided, fall back to a safe development placeholder.
            var adminEmail = config["Seed:AdminEmail"];
            var adminPassword = config["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                adminEmail = "admin@local"; // fallback for dev only
            }

            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                adminPassword = "ChangeMe123!"; // fallback for dev only
            }

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (!result.Succeeded)
                    throw new InvalidOperationException($"Could not create the bootstrap administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }

            if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            {
                var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException($"Could not assign the bootstrap administrator role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
            }

            foreach (var user in userManager.Users.ToList())
            {
                if (await userManager.IsInRoleAsync(user, Roles.Admin))
                    continue;

                var roles = await userManager.GetRolesAsync(user);
                if (roles.Count == 0)
                {
                    var staffResult = await userManager.AddToRoleAsync(user, Roles.Staff);
                    if (!staffResult.Succeeded)
                        throw new InvalidOperationException($"Could not assign the Staff role to '{user.Id}': {string.Join("; ", staffResult.Errors.Select(error => error.Description))}");
                }
            }
        }
    }
}
