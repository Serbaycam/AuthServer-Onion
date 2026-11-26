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

public sealed partial class IdentitySecurityTests : IAsyncLifetime
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
        Assert.Single(responses, response => response.IsSuccessStatusCode);
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
        var legacyId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        // Insert only columns available in the historical schema, independently of the current EF model.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "RefreshTokens" ("Id", "Token", "Expires", "CreatedByIp", "ReplacedByToken", "UserId", "CreatedDate", "IsDeleted")
            VALUES ({legacyId}, {"legacy-plaintext-secret"}, {createdAt.AddDays(1)}, {"test"}, {"legacy-successor-secret"}, {adminId}, {createdAt}, FALSE)
            """);
        await migrator.MigrateAsync();
        db.ChangeTracker.Clear();
        var migrated = await db.RefreshTokens.SingleAsync(t => t.Id == legacyId);
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

    [Fact]
    public async Task JwtLoginAuditFailureDoesNotPersistSession()
    {
        var existing = await Login();
        Guid[] sessionIdsBefore;
        Guid[] auditIdsBefore;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            sessionIdsBefore = await db.RefreshTokens.Select(session => session.Id).ToArrayAsync();
            auditIdsBefore = await db.AuditLogs.Select(log => log.Id).ToArrayAsync();
        }

        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, FailingAuditService>()));
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var response = await failingClient.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = Password });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Simulated audit failure", body);
        Assert.DoesNotContain(Password, body);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(sessionIdsBefore.OrderBy(id => id), (await db.RefreshTokens.Select(session => session.Id).ToArrayAsync()).OrderBy(id => id));
            Assert.Equal(auditIdsBefore.OrderBy(id => id), (await db.AuditLogs.Select(log => log.Id).ToArrayAsync()).OrderBy(id => id));
        }
        Authorize(existing);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task RevokeAllAuditFailureRollsBackRevocation()
    {
        var first = await Login();
        var second = await Login();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, FailingAuditService>()));
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", first.AccessToken);
        using var response = await failingClient.PostAsJsonAsync("/api/usermanagement/revoke-all", new { userId = adminId });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Simulated audit failure", await response.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessions = await db.RefreshTokens.Where(session => session.UserId == adminId).ToListAsync();
            Assert.Equal(2, sessions.Count);
            Assert.All(sessions, session => Assert.Null(session.RevokedDate));
            Assert.False(await db.AuditLogs.AnyAsync(log => log.Action == "RevokeAllTokensCommand"));
        }
        Authorize(first);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        Authorize(second);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task RevokeAllRevokesJwtAndBrowserSessionsAndWritesAudit()
    {
        var targetJwt = await Login();
        using var browserLogin = await BrowserLogin();
        Guid actorId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var actor = new AppUser { UserName = "operator@example.test", Email = "operator@example.test", FirstName = "Operator", LastName = "Test", IsActive = true };
            Assert.True((await users.CreateAsync(actor, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(actor, "SuperAdmin")).Succeeded);
            actorId = actor.Id;
        }
        using var actorClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var actorLogin = await actorClient.PostAsJsonAsync("/api/auth/login", new { email = "operator@example.test", password = Password });
        actorLogin.EnsureSuccessStatusCode();
        var actorToken = (await actorLogin.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        actorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", actorToken.AccessToken);
        using var response = await actorClient.PostAsJsonAsync("/api/usermanagement/revoke-all", new { userId = adminId });
        response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<ServiceResponse<bool>>())!.Succeeded);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var targetSessions = await db.RefreshTokens.Where(session => session.UserId == adminId).ToListAsync();
            Assert.Equal(2, targetSessions.Count);
            Assert.All(targetSessions, session => Assert.NotNull(session.RevokedDate));
            Assert.True(await db.RefreshTokens.AnyAsync(session => session.Id == SessionId(actorToken) && session.UserId == actorId && session.RevokedDate == null));
            var audit = await db.AuditLogs.SingleAsync(log => log.Action == "RevokeAllTokensCommand");
            Assert.Equal(actorId.ToString(), audit.UserId);
            Assert.DoesNotContain(Password, audit.Details);
            Assert.DoesNotContain(targetJwt.RefreshToken, audit.Details);
            Assert.DoesNotContain(actorToken.AccessToken, audit.Details);
        }
        Authorize(targetJwt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(JsonValueKind.Null, (await BrowserState()).GetProperty("user").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await actorClient.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task JwtLoginPersistsSessionAndAuditTogether()
    {
        var token = await Login();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.RefreshTokens.SingleAsync();
            Assert.Equal(SessionId(token), session.Id);
            Assert.Equal(adminId, session.UserId);
            Assert.Equal(RefreshTokenHash.Compute(token.RefreshToken), session.Token);
            Assert.NotEqual(token.RefreshToken, session.Token);
            Assert.Null(session.RevokedDate);
            var audit = await db.AuditLogs.SingleAsync(log => log.Action == "Login");
            Assert.Equal(adminId.ToString(), audit.UserId);
            Assert.Equal(adminId.ToString(), audit.EntityId);
            Assert.DoesNotContain(Password, audit.Details);
            Assert.DoesNotContain(token.RefreshToken, audit.Details);
            Assert.DoesNotContain(token.AccessToken, audit.Details);
        }
        Authorize(token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
    }

    [Fact]
    public async Task AssignRolesAuditFailureKeepsOriginalMembership()
    {
        var token = await Login();
        Guid targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = new AppUser { UserName = "role-rollback@example.test", Email = "role-rollback@example.test", FirstName = "Role", LastName = "Rollback", IsActive = true };
            Assert.True((await users.CreateAsync(target, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(target, "Basic")).Succeeded);
            targetId = target.Id;
        }
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, FailingAuditService>()));
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        failingClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await failingClient.PostAsJsonAsync("/api/usermanagement/assign-roles", new { userId = targetId, roles = new[] { "SuperAdmin" } });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var checkedUsers = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var checkedTarget = (await checkedUsers.FindByIdAsync(targetId.ToString()))!;
        Assert.Equal(new[] { "Basic" }, (await checkedUsers.GetRolesAsync(checkedTarget)).ToArray());
    }

    [Fact]
    public async Task AdministratorCanManageRolePermissionsAndUserLifecycle()
    {
        Authorize(await Login());
        (await client.PostAsJsonAsync("/api/rolemanagement/role", new { roleName = "QualityReviewer" })).EnsureSuccessStatusCode();
        var roles = (await client.GetFromJsonAsync<ServiceResponse<List<RoleDto>>>("/api/rolemanagement/roles"))!.Data;
        var role = Assert.Single(roles, item => item.Name == "QualityReviewer");
        (await client.PutAsJsonAsync("/api/rolemanagement/role", new { roleId = role.Id, newRoleName = "QualityAuditor" })).EnsureSuccessStatusCode();
        var renamedRoles = (await client.GetFromJsonAsync<ServiceResponse<List<RoleDto>>>("/api/rolemanagement/roles"))!.Data;
        Assert.Contains(renamedRoles, item => item.Id == role.Id && item.Name == "QualityAuditor");
        Assert.DoesNotContain(renamedRoles, item => item.Name == "QualityReviewer");
        var permissions = (await client.GetFromJsonAsync<ServiceResponse<List<string>>>("/api/rolemanagement/permissions"))!.Data;
        Assert.Contains("Permissions.Laboratories.View", permissions);
        (await client.PostAsJsonAsync("/api/rolemanagement/permissions", new { roleId = role.Id, permissions = new[] { "Permissions.Laboratories.View" } })).EnsureSuccessStatusCode();
        var assignedPermissions = (await client.GetFromJsonAsync<ServiceResponse<List<PermissionDto>>>("/api/rolemanagement/role-permissions/" + role.Id))!.Data;
        Assert.Equal("Permissions.Laboratories.View", Assert.Single(assignedPermissions).PermissionName);

        using var createUser = await client.PostAsJsonAsync("/api/usermanagement/create-user", new
        { email = "lifecycle@example.test", firstName = "Original", lastName = "User", password = Password, roles = new[] { "QualityAuditor" } });
        createUser.EnsureSuccessStatusCode();
        var targetId = (await createUser.Content.ReadFromJsonAsync<ServiceResponse<Guid>>())!.Data;
        (await client.PutAsJsonAsync("/api/usermanagement/update-user", new
        { userId = targetId, email = "updated-lifecycle@example.test", firstName = "Updated", lastName = "User" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/usermanagement/assign-roles", new { userId = targetId, roles = new[] { "Basic", "QualityAuditor" } })).EnsureSuccessStatusCode();
        var listedUsers = (await client.GetFromJsonAsync<ServiceResponse<List<UserWithRolesDto>>>("/api/usermanagement/all-users"))!.Data;
        var listedTarget = Assert.Single(listedUsers, user => user.Id == targetId);
        Assert.Equal("Updated User", listedTarget.FullName);
        Assert.Equal("updated-lifecycle@example.test", listedTarget.Email);
        Assert.Equal(new[] { "Basic", "QualityAuditor" }, listedTarget.Roles.OrderBy(name => name).ToArray());

        using var firstTargetLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = listedTarget.Email, password = Password });
        firstTargetLogin.EnsureSuccessStatusCode();
        var firstTargetToken = (await firstTargetLogin.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        (await client.PostAsJsonAsync("/api/usermanagement/update-status", new { userId = targetId, isActive = false })).EnsureSuccessStatusCode();
        using var targetClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstTargetToken.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = listedTarget.Email, password = Password })).StatusCode);
        (await client.PostAsJsonAsync("/api/usermanagement/update-status", new { userId = targetId, isActive = true })).EnsureSuccessStatusCode();
        using var reactivatedLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = listedTarget.Email, password = Password });
        reactivatedLogin.EnsureSuccessStatusCode();
        var reactivatedToken = (await reactivatedLogin.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        const string newPassword = "Lifecycle-new-password-593!";
        (await client.PostAsJsonAsync("/api/usermanagement/change-password", new { userId = targetId, newPassword })).EnsureSuccessStatusCode();
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reactivatedToken.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = reactivatedToken.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = listedTarget.Email, password = Password })).StatusCode);
        using var newTargetLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = listedTarget.Email, password = newPassword });
        newTargetLogin.EnsureSuccessStatusCode();
        var newTargetToken = (await newTargetLogin.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        (await client.PostAsJsonAsync("/api/usermanagement/assign-roles", new { userId = targetId, roles = new[] { "Basic" } })).EnsureSuccessStatusCode();
        (await client.DeleteAsync("/api/rolemanagement/role/" + role.Id)).EnsureSuccessStatusCode();
        var sessions = (await client.GetFromJsonAsync<ServiceResponse<List<ActiveSessionDto>>>("/api/sessionmanagement/active-sessions"))!.Data;
        Assert.Contains(sessions, session => session.TokenId == SessionId(newTargetToken) && !session.IsCurrentSession);
        (await client.PostAsJsonAsync("/api/sessionmanagement/kill-session", new { tokenId = SessionId(newTargetToken) })).EnsureSuccessStatusCode();
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newTargetToken.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        var stats = (await client.GetFromJsonAsync<ServiceResponse<DashboardStatsDto>>("/api/dashboard/stats"))!.Data;
        Assert.Equal(2, stats.TotalUsers);
        Assert.Equal(2, stats.TotalRoles);
        Assert.Equal(1, stats.TotalActiveSessions);
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Roles.AnyAsync(item => item.Id.ToString() == role.Id));
        var users = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var target = (await users.FindByIdAsync(targetId.ToString()))!;
        Assert.True(target.IsActive);
        Assert.Equal(listedTarget.Email, target.UserName);
        Assert.Equal(new[] { "Basic" }, (await users.GetRolesAsync(target)).ToArray());
        Assert.False(await db.RefreshTokens.AnyAsync(session => session.UserId == targetId && session.RevokedDate == null));
    }

    [Fact]
    public async Task AnonymousAndBasicUsersCannotUseAnyManagementEndpoint()
    {
        Guid basicId;
        string roleId;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var basic = new AppUser { UserName = "basic@example.test", Email = "basic@example.test", FirstName = "Basic", LastName = "Test", IsActive = true };
            Assert.True((await users.CreateAsync(basic, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(basic, "Basic")).Succeeded);
            basicId = basic.Id;
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            var role = new AppRole { Name = "DeniedFixture" };
            Assert.True((await roles.CreateAsync(role)).Succeeded);
            roleId = role.Id.ToString();
        }
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "basic@example.test", password = Password });
        login.EnsureSuccessStatusCode();
        var basicToken = (await login.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        var endpoints = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/usermanagement/all-users", null),
            (HttpMethod.Post, "/api/usermanagement/create-user", new { email = "denied-create@example.test", firstName = "Denied", lastName = "Create", password = Password, roles = new[] { "Basic" } }),
            (HttpMethod.Put, "/api/usermanagement/update-user", new { userId = basicId, email = "denied-update@example.test", firstName = "Denied", lastName = "Update" }),
            (HttpMethod.Post, "/api/usermanagement/change-password", new { userId = basicId, newPassword = "Denied-change-password-593!" }),
            (HttpMethod.Post, "/api/usermanagement/assign-roles", new { userId = basicId, roles = new[] { "SuperAdmin" } }),
            (HttpMethod.Post, "/api/usermanagement/update-status", new { userId = basicId, isActive = false }),
            (HttpMethod.Post, "/api/usermanagement/revoke-all", new { userId = basicId }),
            (HttpMethod.Get, "/api/rolemanagement/roles", null),
            (HttpMethod.Post, "/api/rolemanagement/role", new { roleName = "DeniedCreateRole" }),
            (HttpMethod.Put, "/api/rolemanagement/role", new { roleId, newRoleName = "DeniedRenamedRole" }),
            (HttpMethod.Delete, "/api/rolemanagement/role/" + roleId, null),
            (HttpMethod.Get, "/api/rolemanagement/permissions", null),
            (HttpMethod.Post, "/api/rolemanagement/permissions", new { roleId, permissions = new[] { "Permissions.Laboratories.View" } }),
            (HttpMethod.Get, "/api/rolemanagement/role-permissions/" + roleId, null),
            (HttpMethod.Get, "/api/sessionmanagement/active-sessions", null),
            (HttpMethod.Post, "/api/sessionmanagement/kill-session", new { tokenId = SessionId(basicToken) }),
            (HttpMethod.Get, "/api/dashboard/stats", null)
        };
        foreach (var expectedStatus in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
        {
            client.DefaultRequestHeaders.Authorization = expectedStatus == HttpStatusCode.Unauthorized
                ? null : new AuthenticationHeaderValue("Bearer", basicToken.AccessToken);
            foreach (var endpoint in endpoints)
            {
                using var request = new HttpRequestMessage(endpoint.Method, endpoint.Path);
                if (endpoint.Body != null) request.Content = JsonContent.Create(endpoint.Body);
                using var response = await client.SendAsync(request);
                Assert.True(response.StatusCode == expectedStatus,
                    $"{endpoint.Method} {endpoint.Path}: expected {expectedStatus}, received {response.StatusCode}.");
            }
        }
        using var check = factory.Services.CreateScope();
        var checkedUsers = check.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var checkedBasic = (await checkedUsers.FindByIdAsync(basicId.ToString()))!;
        Assert.True(checkedBasic.IsActive);
        Assert.Equal("basic@example.test", checkedBasic.Email);
        Assert.Equal(new[] { "Basic" }, (await checkedUsers.GetRolesAsync(checkedBasic)).ToArray());
        Assert.Null(await checkedUsers.FindByEmailAsync("denied-create@example.test"));
        var checkedRoles = check.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        Assert.Equal("DeniedFixture", (await checkedRoles.FindByIdAsync(roleId))!.Name);
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.RefreshTokens.AnyAsync(session => session.Id == SessionId(basicToken) && session.RevokedDate == null));
    }

    [Fact]
    public async Task AuthenticationRateLimitRejectsTheThirtyFirstRequest()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, 31).Select(_ =>
            client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "not-a-real-password" })));
        try
        {
            Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests }));
            Assert.Equal(30, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.TooManyRequests);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
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
