using System.Net;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.AspNetCore.TestHost;
using AuthServer.Identity.Application.Interfaces;
using Xunit;

namespace AuthServer.Identity.Tests;

public sealed class IdentitySecurityTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> factory;
    private HttpClient client = null!;
    private const string Password = "Test-only-password-593!";
    private Guid adminId;

    public IdentitySecurityTests()
    {
        var connection = Environment.GetEnvironmentVariable("AUTH_TEST_DATABASE")
            ?? throw new InvalidOperationException("Set AUTH_TEST_DATABASE to a disposable PostgreSQL database ending in _tests.");
        if (new NpgsqlConnectionStringBuilder(connection).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Test database name must end in _tests; tests delete and recreate it.");
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connection,
                ["JwtSettings:Secret"] = "TEST-ONLY-random-signing-key-not-for-production-1234567890",
                ["JwtSettings:Issuer"] = "tests",
                ["JwtSettings:Audience"] = "tests",
                ["Database:Initialize"] = "false"
            }));
        });
    }

    public async Task InitializeAsync()
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        Assert.True((await roles.CreateAsync(new AppRole { Name = "SuperAdmin" })).Succeeded);
        Assert.True((await roles.CreateAsync(new AppRole { Name = "Basic" })).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = new AppUser { UserName = "admin@example.test", Email = "admin@example.test", IsActive = true, FirstName = "Admin", LastName = "Test" };
        Assert.True((await users.CreateAsync(admin, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, "SuperAdmin")).Succeeded);
        adminId = admin.Id;
    }

    private async Task<TokenDto> Login()
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = Password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
    }
    private void Authorize(TokenDto token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

    private static Guid SessionId(TokenDto token) => Guid.Parse(new JwtSecurityTokenHandler()
        .ReadJwtToken(token.AccessToken).Claims.Single(claim => claim.Type == "sid").Value);

    private async Task<TokenDto> Rotate(TokenDto token)
    {
        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token.RefreshToken });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
    }

    [Fact]
    public async Task ExpiredAccessTokenCanRefreshWithoutLeavingItsOldSessionActive()
    {
        var first = await Login();
        var independent = await Login();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(first.AccessToken);
        var expired = new JwtSecurityToken("tests", "tests", jwt.Claims, expires: DateTime.UtcNow.AddMinutes(-1),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                "TEST-ONLY-random-signing-key-not-for-production-1234567890")), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(expired));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        var next = await Rotate(first); // Expired Authorization header must not prevent refresh.
        Authorize(next);
        var listed = (await client.GetFromJsonAsync<ServiceResponse<List<ActiveSessionDto>>>("/api/sessionmanagement/active-sessions"))!.Data;
        Assert.Equal(2, listed.Count); // One rotated session and a separate login; not two versions of the first.
        Assert.DoesNotContain(listed, item => item.TokenId == SessionId(first));
        Assert.Contains(listed, item => item.TokenId == SessionId(next) && item.IsCurrentSession);
        Assert.Contains(listed, item => item.TokenId == SessionId(independent) && !item.IsCurrentSession);
        Authorize(first); // Also reject the original, cryptographically unexpired access token.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var old = await db.RefreshTokens.SingleAsync(item => item.Id == SessionId(first));
        Assert.NotNull(old.RevokedDate);
        Assert.Equal(RefreshTokenHash.Compute(next.RefreshToken), old.ReplacedByToken);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(item => item.RevokedDate == null && item.Expires > DateTime.UtcNow));
    }

    [Fact]
    public async Task AdministratorClosingAStaleListEntryAlsoClosesItsRotatedSuccessor()
    {
        var first = await Login();
        var independent = await Login();
        var current = await Rotate(await Rotate(first));
        Authorize(independent);
        var response = await client.PostAsJsonAsync("/api/sessionmanagement/kill-session", new { tokenId = SessionId(first) });
        response.EnsureSuccessStatusCode();
        Authorize(current);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        Authorize(independent);
        var listed = (await client.GetFromJsonAsync<ServiceResponse<List<ActiveSessionDto>>>("/api/sessionmanagement/active-sessions"))!.Data;
        Assert.Single(listed);
        Assert.Equal(SessionId(independent), listed[0].TokenId);
        // Retrying the old action is idempotent and does not kill unrelated logins.
        (await client.PostAsJsonAsync("/api/sessionmanagement/kill-session", new { tokenId = SessionId(first) })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task LogoutUsingAPreRotationCredentialRevokesItsSuccessorOnly()
    {
        var first = await Login();
        var independent = await Login();
        var current = await Rotate(await Rotate(first));
        var response = await client.PostAsJsonAsync("/api/auth/revoke-token", new { token = first.RefreshToken });
        response.EnsureSuccessStatusCode();
        Authorize(current);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        Authorize(independent);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        (await client.PostAsJsonAsync("/api/auth/revoke-token", new { token = first.RefreshToken })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task EnablingMfaBlocksBothExistingSingleFactorAccessAndRefresh()
    {
        var token = await Login();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        }
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Authorize(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task RevokingOneSessionDoesNotLeaveItsAccessTokenValidThroughAnotherSession()
    {
        var first = await Login();
        var second = await Login();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.RefreshTokens.AnyAsync(t => t.Token == first.RefreshToken));
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.Token == RefreshTokenHash.Compute(first.RefreshToken)));
        }
        (await client.PostAsJsonAsync("/api/auth/revoke-token", new { token = first.RefreshToken })).EnsureSuccessStatusCode();
        Authorize(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        Authorize(second);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task RotationIsSingleUseAndReplayRevokesTheSuccessor()
    {
        var first = await Login();
        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = first.RefreshToken });
        response.EnsureSuccessStatusCode();
        var next = (await response.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        Assert.NotEqual(first.RefreshToken, next.RefreshToken);
        Authorize(first);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = first.RefreshToken })).StatusCode);
        Authorize(next);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRefreshNeverIssuesTwoSuccessfulSuccessors()
    {
        var first = await Login();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = first.RefreshToken })));
        Assert.Single(responses.Where(r => r.IsSuccessStatusCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RefreshTokens.CountAsync(t => t.RevokedDate == null) <= 1);
    }

    [Fact]
    public async Task LastAdminIsProtectedAndFailedRoleCreationRollsBackUser()
    {
        Authorize(await Login());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/usermanagement/update-status", new { userId = adminId, isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/usermanagement/assign-roles", new { userId = adminId, roles = new[] { "Basic" } })).StatusCode);
        var response = await client.PostAsJsonAsync("/api/usermanagement/create-user", new
        {
            email = "new@example.test", firstName = "New", lastName = "Test", password = Password, roles = new[] { "MissingRole" }
        });
        Assert.False(response.IsSuccessStatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync("new@example.test"));
    }

    [Fact]
    public async Task RoleRemovalImmediatelyOverridesJwtRoleAndInactiveUsersCannotRefresh()
    {
        var first = await Login();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "SuperAdmin")).Succeeded);
        }
        Authorize(first);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            user.IsActive = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = first.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task InvalidInputIsRejectedAndFiveBadPasswordsLockTheAccount()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { })).StatusCode);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = Password })).StatusCode);
    }

    [Fact]
    public async Task AuditFailureRollsBackUserAndRoleAssignment()
    {
        var token = await Login();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, FailingAuditService>()));
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var response = await failingClient.PostAsJsonAsync("/api/usermanagement/create-user", new
        {
            email = "rollback@example.test", firstName = "Rollback", lastName = "Test", password = Password, roles = new[] { "Basic" }
        });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Simulated audit failure", await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>().FindByEmailAsync("rollback@example.test"));
    }

    [Fact]
    public async Task SecurityMigrationErasesLegacySecretsWithoutDeletingUsers()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20260420180544_InitialPostgres");
        var legacy = new RefreshToken { Token = "legacy-plaintext-secret", ReplacedByToken = "legacy-successor-secret",
            UserId = adminId, CreatedByIp = "test", Expires = DateTime.UtcNow.AddDays(1) };
        db.RefreshTokens.Add(legacy);
        await db.SaveChangesAsync();
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var migrated = await db.RefreshTokens.SingleAsync(t => t.Id == legacy.Id);
        Assert.NotNull(migrated.RevokedDate);
        Assert.NotEqual("legacy-plaintext-secret", migrated.Token);
        Assert.Null(migrated.ReplacedByToken);
        Assert.True(await db.Users.AnyAsync(u => u.Id == adminId));
    }

    private async Task<JsonElement> BrowserState()
    {
        var response = await client.GetAsync("/api/admin-session");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
    }

    private async Task<HttpResponseMessage> BrowserLogin()
    {
        var state = await BrowserState();
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", state.GetProperty("csrfToken").GetString());
        var response = await client.PostAsJsonAsync("/api/admin-session/login", new { email = "admin@example.test", password = Password });
        response.EnsureSuccessStatusCode();
        var signedIn = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        client.DefaultRequestHeaders.Add("X-CSRF-Token", signedIn.GetProperty("csrfToken").GetString());
        return response;
    }

    [Fact]
    public async Task BrowserCookieRestoresSessionWithoutExposingBearerCredentials()
    {
        using var response = await BrowserLogin();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("AuthServer.AdminSession="));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("secure", cookie.ToLowerInvariant());
        Assert.Contains("samesite=strict", cookie.ToLowerInvariant());
        Assert.Contains("expires=", cookie.ToLowerInvariant());
        Assert.DoesNotContain("accessToken", await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain("refreshToken", await response.Content.ReadAsStringAsync());

        // A fresh page/tab can bootstrap from the cookie alone; no JS storage or Authorization header.
        using var reloaded = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        reloaded.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        var restored = await reloaded.GetFromJsonAsync<JsonElement>("/api/admin-session");
        Assert.Equal(adminId.ToString(), restored.GetProperty("data").GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.OK, (await reloaded.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task CookieMutationsRequireCsrfAndLogoutSurvivesPageReload()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin-session/login", new { email = "admin@example.test", password = Password })).StatusCode);
        await BrowserLogin();
        var valid = client.DefaultRequestHeaders.GetValues("X-CSRF-Token").Single();
        client.DefaultRequestHeaders.Remove("X-CSRF-Token");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/rolemanagement/role", new { roleName = "Rejected" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/admin-session/logout", new { })).StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", valid);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/rolemanagement/role", new { roleName = "Allowed" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/admin-session/logout", new { })).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await BrowserState()).GetProperty("user").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == adminId && t.RevokedDate == null));
        Assert.False(await db.Roles.AnyAsync(role => role.Name == "Rejected"));
    }

    [Fact]
    public async Task RevokedBrowserCookieCannotRestoreAndDemotedAdministratorLosesAccess()
    {
        await BrowserLogin();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "SuperAdmin")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var session in await db.RefreshTokens.Where(t => t.UserId == adminId).ToListAsync()) session.RevokedDate = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Equal(JsonValueKind.Null, (await BrowserState()).GetProperty("user").ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task BrowserRejectsNonAdminLoginWithoutCreatingSession()
    {
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            Assert.True((await users.RemoveFromRoleAsync(user, "SuperAdmin")).Succeeded);
        }
        var state = await BrowserState();
        client.DefaultRequestHeaders.Add("X-CSRF-Token", state.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/admin-session/login", new { email = "admin@example.test", password = Password })).StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpsProxySchemeRequiresAnExplicitlyTrustedPeer(bool trusted)
    {
        await using var proxyFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, ProxyPeerFilter>();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                if (trusted) options.KnownProxies.Add(IPAddress.Parse("192.0.2.10"));
            });
        }));
        using var proxyClient = proxyFactory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        proxyClient.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        var response = await proxyClient.GetAsync("/api/admin-session");
        Assert.Equal(trusted ? HttpStatusCode.OK : HttpStatusCode.InternalServerError, response.StatusCode);
        if (trusted)
            Assert.Contains(response.Headers.GetValues("Set-Cookie"), value => value.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class ProxyPeerFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, runNext) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
                await runNext(context);
            });
            next(app);
        };
    }

    private sealed class FailingAuditService : IAuditService
    {
        public Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress)
            => throw new InvalidOperationException("Simulated audit failure");
    }

    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
}
