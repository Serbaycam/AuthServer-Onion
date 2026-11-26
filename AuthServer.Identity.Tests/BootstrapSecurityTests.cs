using AuthServer.Identity.Domain.Constants;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Domain.Enums;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Xunit;

namespace AuthServer.Identity.Tests;

// A partial keeps these database-resetting tests in the existing xUnit class,
// which prevents them from running alongside the other identity tests.
public sealed partial class IdentitySecurityTests
{
    [Fact]
    public async Task BootstrapRoleAssignmentFailureRollsBackUserAndRoleSeed()
    {
        var before = await CaptureBootstrapStateAsync();
        await using var bootstrap = CreateBootstrapFactory("bootstrap-new@example.test", Password, rejectRoleAssignment: true);

        var failure = Assert.ThrowsAny<Exception>(() =>
        {
            using var unused = bootstrap.CreateClient();
        });
        Assert.Contains("Simulated bootstrap role assignment failure", failure.ToString());
        Assert.DoesNotContain(Password, failure.ToString());

        var after = await CaptureBootstrapStateAsync();
        Assert.Equal(before.UserIds, after.UserIds);
        Assert.Equal(before.RoleNames, after.RoleNames);
    }

    [Fact]
    public async Task BootstrapExistingNonAdministratorFailsWithoutChangingAccount()
    {
        const string email = "bootstrap-existing@example.test";
        const string replacementPassword = "Different-bootstrap-password-832!";
        Guid existingId;
        string? originalPasswordHash;
        string? originalStamp;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = new AppUser
            {
                UserName = email, Email = email, FirstName = "Existing", LastName = "Account", IsActive = true
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, Roles.Basic.ToString())).Succeeded);
            existingId = user.Id;
            originalPasswordHash = user.PasswordHash;
            originalStamp = user.SecurityStamp;
        }
        var before = await CaptureBootstrapStateAsync();
        await using var bootstrap = CreateBootstrapFactory(email, replacementPassword);

        var failure = Assert.ThrowsAny<Exception>(() =>
        {
            using var unused = bootstrap.CreateClient();
        });
        Assert.Contains("existing", failure.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SuperAdmin", failure.ToString());
        Assert.DoesNotContain(replacementPassword, failure.ToString());

        var after = await CaptureBootstrapStateAsync();
        Assert.Equal(before.UserIds, after.UserIds);
        Assert.Equal(before.RoleNames, after.RoleNames);
        using var check = factory.Services.CreateScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var unchanged = (await manager.FindByIdAsync(existingId.ToString()))!;
        Assert.Equal(originalPasswordHash, unchanged.PasswordHash);
        Assert.Equal(originalStamp, unchanged.SecurityStamp);
        Assert.Equal(new[] { Roles.Basic.ToString() }, await manager.GetRolesAsync(unchanged));
        Assert.True(await manager.CheckPasswordAsync(unchanged, Password));
        Assert.False(await manager.CheckPasswordAsync(unchanged, replacementPassword));
    }

    [Theory]
    [InlineData("bootstrap-incomplete@example.test", "")]
    [InlineData("", "Test-only-password-593!")]
    public async Task BootstrapIncompleteCredentialsRollBackRoleSeed(string email, string password)
    {
        var before = await CaptureBootstrapStateAsync();
        await using var bootstrap = CreateBootstrapFactory(email, password);

        var failure = Assert.ThrowsAny<Exception>(() =>
        {
            using var unused = bootstrap.CreateClient();
        });
        Assert.Contains("Both bootstrap administrator email and password are required", failure.ToString());
        if (!string.IsNullOrEmpty(password)) Assert.DoesNotContain(password, failure.ToString());

        var after = await CaptureBootstrapStateAsync();
        Assert.Equal(before.UserIds, after.UserIds);
        Assert.Equal(before.RoleNames, after.RoleNames);
    }

    [Fact]
    public async Task BootstrapIsIdempotentWithoutResettingPasswordOrRoleClaims()
    {
        const string email = "bootstrap-idempotent@example.test";
        const string replacementPassword = "Different-bootstrap-password-943!";
        await using (var bootstrap = CreateBootstrapFactory(email, Password))
        {
            using var started = bootstrap.CreateClient();
        }

        Guid userId;
        string? originalPasswordHash;
        string? originalStamp;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByEmailAsync(email))!;
            userId = user.Id;
            originalPasswordHash = user.PasswordHash;
            originalStamp = user.SecurityStamp;
            Assert.True(await users.IsInRoleAsync(user, Roles.SuperAdmin.ToString()));
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            var lab = (await roles.FindByNameAsync(Roles.LabManager.ToString()))!;
            Assert.True((await roles.AddClaimAsync(lab, new Claim("Permission", Permissions.Laboratories.View))).Succeeded);
        }
        var before = await CaptureBootstrapStateAsync();
        string[] claimsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var claims = await db.RoleClaims.AsNoTracking().ToArrayAsync();
            claimsBefore = claims.Select(claim => $"{claim.RoleId}|{claim.ClaimType}|{claim.ClaimValue}").OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        await using (var bootstrap = CreateBootstrapFactory(email, replacementPassword))
        {
            using var started = bootstrap.CreateClient();
        }

        var after = await CaptureBootstrapStateAsync();
        Assert.Equal(before.UserIds, after.UserIds);
        Assert.Equal(before.RoleNames, after.RoleNames);
        Assert.Equal(Enum.GetNames<Roles>().OrderBy(name => name, StringComparer.Ordinal), after.RoleNames);
        using var check = factory.Services.CreateScope();
        var manager = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var unchanged = (await manager.FindByEmailAsync(email))!;
        Assert.Equal(userId, unchanged.Id);
        Assert.Equal(originalPasswordHash, unchanged.PasswordHash);
        Assert.Equal(originalStamp, unchanged.SecurityStamp);
        Assert.True(await manager.IsInRoleAsync(unchanged, Roles.SuperAdmin.ToString()));
        Assert.True(await manager.CheckPasswordAsync(unchanged, Password));
        Assert.False(await manager.CheckPasswordAsync(unchanged, replacementPassword));
        var persistedClaims = await check.ServiceProvider.GetRequiredService<AppDbContext>().RoleClaims.AsNoTracking().ToArrayAsync();
        Assert.Equal(claimsBefore, persistedClaims.Select(claim => $"{claim.RoleId}|{claim.ClaimType}|{claim.ClaimValue}").OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public async Task BootstrapWithoutCredentialsOnlySeedsMissingRoles()
    {
        var before = await CaptureBootstrapStateAsync();
        await using (var bootstrap = CreateBootstrapFactory("", ""))
        {
            using var started = bootstrap.CreateClient();
        }

        var after = await CaptureBootstrapStateAsync();
        Assert.Equal(before.UserIds, after.UserIds);
        Assert.Equal(Enum.GetNames<Roles>().OrderBy(name => name, StringComparer.Ordinal), after.RoleNames);
        using var check = factory.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<AppDbContext>().RoleClaims.AnyAsync());
    }

    private WebApplicationFactory<Program> CreateBootstrapFactory(string email, string password, bool rejectRoleAssignment = false)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Initialize"] = "true",
                ["Bootstrap:AdminEmail"] = email,
                ["Bootstrap:AdminPassword"] = password
            }));
            if (rejectRoleAssignment)
                builder.ConfigureTestServices(services => services.AddScoped<UserManager<AppUser>, RejectBootstrapRoleUserManager>());
        });

    private async Task<(Guid[] UserIds, string[] RoleNames)> CaptureBootstrapStateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.AsNoTracking().Select(user => user.Id).ToArrayAsync();
        var roles = await db.Roles.AsNoTracking().Select(role => role.Name!).ToArrayAsync();
        return (users.OrderBy(id => id).ToArray(), roles.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private sealed class RejectBootstrapRoleUserManager(
        IUserStore<AppUser> store,
        IOptions<IdentityOptions> options,
        IPasswordHasher<AppUser> passwordHasher,
        IEnumerable<IUserValidator<AppUser>> userValidators,
        IEnumerable<IPasswordValidator<AppUser>> passwordValidators,
        ILookupNormalizer normalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<AppUser>> logger)
        : UserManager<AppUser>(store, options, passwordHasher, userValidators, passwordValidators, normalizer, errors, services, logger)
    {
        public override Task<IdentityResult> AddToRoleAsync(AppUser user, string role)
            => Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "BootstrapRoleAssignmentRejected",
                Description = "Simulated bootstrap role assignment failure"
            }));
    }
}
