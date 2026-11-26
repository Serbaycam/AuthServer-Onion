using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace AuthServer.Identity.Persistence.Seeds;

public static class ContextSeed
{
    // Migrations are a separate step. Identity saves each operation itself, so
    // one transaction must cover both missing roles and the bootstrap account.
    public static async Task SeedAsync(AppDbContext db, UserManager<AppUser> users, RoleManager<AppRole> roles,
        IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await SeedRolesAsync(roles);
        await SeedSuperAdminAsync(users, configuration);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedRolesAsync(RoleManager<AppRole> roles)
    {
        foreach (var name in Enum.GetNames<Roles>())
            if (!await roles.RoleExistsAsync(name)) Ensure(await roles.CreateAsync(new AppRole { Name = name }));
    }

    private static async Task SeedSuperAdminAsync(UserManager<AppUser> users, IConfiguration configuration)
    {
        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Both bootstrap administrator email and password are required.");
        // Never elevate an existing account just because its email matches configuration.
        var existing = await users.FindByEmailAsync(email);
        if (existing != null)
        {
            if (!await users.IsInRoleAsync(existing, Roles.SuperAdmin.ToString()))
                throw new InvalidOperationException("Bootstrap email matches an existing account without the SuperAdmin role. No account changes were applied.");
            return;
        }
        var user = new AppUser { UserName = email, Email = email, FirstName = "System", LastName = "Administrator", IsActive = true, EmailConfirmed = true };
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddToRoleAsync(user, Roles.SuperAdmin.ToString()));
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
