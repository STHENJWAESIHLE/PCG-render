using Microsoft.AspNetCore.Identity;
using PCG.Authorization;
using PCG.Models;

namespace PCG.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var r in RoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new IdentityRole(r));
        }

        const string demoPassword = "DemoPcg2026!";

        async Task EnsureUser(string email, string role, string? display = null)
        {
            var u = await userManager.FindByEmailAsync(email);
            if (u != null) return;
            u = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = display ?? email.Split('@')[0]
            };
            var res = await userManager.CreateAsync(u, demoPassword);
            if (!res.Succeeded)
                throw new InvalidOperationException($"Seed user {email}: " + string.Join("; ", res.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(u, role);
        }

        await EnsureUser("admin@pcg.demo", RoleNames.Admin, "Admin User");
        await EnsureUser("reviewer@pcg.demo", RoleNames.Reviewer, "Reviewer");
        await EnsureUser("manager@pcg.demo", RoleNames.Manager, "Manager");
        await EnsureUser("external@pcg.demo", RoleNames.ExternalUser, "External Customer");
        await EnsureUser("viewer@pcg.demo", RoleNames.Viewer, "Viewer");
    }
}
