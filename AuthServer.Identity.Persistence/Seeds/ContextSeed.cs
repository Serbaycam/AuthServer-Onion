using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace AuthServer.Identity.Persistence.Seeds;

public static class ContextSeed
{
    public static async Task SeedRolesAsync(UserManager<AppUser> users, RoleManager<AppRole> roles)
    {
        foreach (var name in Enum.GetNames<Roles>())
            if (!await roles.RoleExistsAsync(name)) Ensure(await roles.CreateAsync(new AppRole { Name = name }));
    }

    public static async Task SeedSuperAdminAsync(UserManager<AppUser> users, RoleManager<AppRole> roles, IConfiguration configuration)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Both bootstrap administrator email and password are required.");
        // Never elevate an existing account just because its email matches configuration.
        if (await users.FindByEmailAsync(email) != null) return;
        var user = new AppUser { UserName = email, Email = email, FirstName = "System", LastName = "Administrator", IsActive = true, EmailConfirmed = true };
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddToRoleAsync(user, Roles.SuperAdmin.ToString()));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
