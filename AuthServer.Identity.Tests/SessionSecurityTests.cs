using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Infrastructure.Services;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace AuthServer.Identity.Tests;

public sealed partial class IdentitySecurityTests
{
    [Fact]
    public async Task SecurityStampChangeRejectsExistingRefreshCredential()
    {
        var token = await Login();
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(adminId.ToString()))!;
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Null((await db.RefreshTokens.SingleAsync(session => session.Id == SessionId(token))).RevokedDate);
        }
        Authorize(token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Assert.False((await refresh.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Succeeded);
    }

    [Fact]
    public async Task ConcurrentLoginCannotSurviveRevokeAll()
        => await AssertConcurrentLoginIsInvalidatedAsync(resetPassword: false);

    [Fact]
    public async Task ConcurrentLoginCannotSurvivePasswordReset()
        => await AssertConcurrentLoginIsInvalidatedAsync(resetPassword: true);

    [Fact]
    public async Task RefreshRotationsKeepTheOriginalThirtyDayFamilyDeadline()
    {
        var before = DateTime.UtcNow;
        var first = await Login();
        var after = DateTime.UtcNow;
        var second = await Rotate(first);
        var third = await Rotate(second);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.RefreshTokens.OrderBy(session => session.CreatedDate).ToListAsync();
        Assert.Equal(3, rows.Count);
        var original = Assert.Single(rows, session => session.Id == SessionId(first));
        Assert.NotEqual(Guid.Empty, original.FamilyId);
        Assert.InRange(original.FamilyCreatedAt, before.AddMilliseconds(-1), after);
        Assert.Equal(original.FamilyCreatedAt.AddDays(30), original.AbsoluteExpiresAt);
        var user = await db.Users.SingleAsync(item => item.Id == adminId);
        Assert.Equal(user.SecurityStamp, original.SecurityStamp);
        Assert.All(rows, session =>
        {
            Assert.Equal(original.FamilyId, session.FamilyId);
            Assert.Equal(original.FamilyCreatedAt, session.FamilyCreatedAt);
            Assert.Equal(original.AbsoluteExpiresAt, session.AbsoluteExpiresAt);
            Assert.Equal(original.SecurityStamp, session.SecurityStamp);
            Assert.True(session.Expires <= session.AbsoluteExpiresAt);
        });
        Assert.NotNull(original.RevokedDate);
        Assert.NotNull(Assert.Single(rows, session => session.Id == SessionId(second)).RevokedDate);
        Assert.Null(Assert.Single(rows, session => session.Id == SessionId(third)).RevokedDate);
    }

    [Fact]
    public async Task RefreshNearTheFamilyDeadlineCapsBothJwtAndRefreshExpiration()
    {
        var first = await Login();
        DateTime deadline;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.RefreshTokens.SingleAsync(session => session.Id == SessionId(first));
            row.AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(2);
            await db.SaveChangesAsync();
            await db.Entry(row).ReloadAsync();
            deadline = row.AbsoluteExpiresAt;
        }
        var rotated = await Rotate(first);
        Assert.Equal(deadline, rotated.AccessTokenExpiration);
        Assert.Equal(deadline, rotated.RefreshTokenExpiration);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(rotated.AccessToken);
        Assert.InRange(jwt.ValidTo, deadline.AddSeconds(-1), deadline);
        using var check = factory.Services.CreateScope();
        var session = await check.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.SingleAsync(row => row.Id == SessionId(rotated));
        Assert.Equal(deadline, session.AbsoluteExpiresAt);
        Assert.Equal(deadline, session.Expires);
    }

    [Fact]
    public async Task AbsoluteExpirationRejectsAccessAndRefreshAndRemovesTheActiveSession()
    {
        Authorize(await Login());
        var targetId = await CreateSessionSecurityUserAsync("absolute-expiry@example.test");
        using var login = await client.PostAsJsonAsync("/api/auth/login", new { email = "absolute-expiry@example.test", password = Password });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.RefreshTokens.SingleAsync(session => session.Id == SessionId(token));
            row.AbsoluteExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            row.FamilyCreatedAt = row.AbsoluteExpiresAt.AddDays(-30);
            row.Expires = DateTime.UtcNow.AddDays(1);
            await db.SaveChangesAsync();
        }
        var active = (await client.GetFromJsonAsync<ServiceResponse<List<ActiveSessionDto>>>("/api/sessionmanagement/active-sessions"))!.Data;
        Assert.DoesNotContain(active, session => session.TokenId == SessionId(token));
        var stats = (await client.GetFromJsonAsync<ServiceResponse<DashboardStatsDto>>("/api/dashboard/stats"))!.Data;
        Assert.Equal(1, stats.TotalActiveSessions);
        using var targetClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        using var refresh = await targetClient.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        using var check = factory.Services.CreateScope();
        Assert.True(await check.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(user => user.Id == targetId));
    }

    [Fact]
    public async Task SessionUpgradeInvalidatesLegacyJwtAndCookieWithoutLettingOldSecretsCloseNewLogins()
    {
        var legacyJwt = await Login();
        using var legacyBrowserLogin = await BrowserLogin();
        var jwtId = SessionId(legacyJwt);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var sessions = await db.RefreshTokens.AsNoTracking().ToListAsync();
            var cookie = Assert.Single(sessions, session => session.Id != jwtId);
            var jwt = Assert.Single(sessions, session => session.Id == jwtId);
            Assert.Equal(RefreshTokenHash.Compute(legacyJwt.RefreshToken), jwt.Token);
            var auditIds = await db.AuditLogs.Select(log => log.Id).ToArrayAsync();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20260910090000_HardenRefreshTokens");
            // Write the old schema explicitly; the current EF model contains columns that did not exist yet.
            await db.Database.ExecuteSqlRawAsync("DELETE FROM \"RefreshTokens\"");
            foreach (var legacy in new[] { jwt, cookie })
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "RefreshTokens"
                        ("Id", "Token", "Expires", "CreatedByIp", "UserId", "CreatedDate", "IsDeleted")
                    VALUES ({legacy.Id}, {legacy.Token}, {legacy.Expires}, {legacy.CreatedByIp}, {legacy.UserId}, {legacy.CreatedDate}, false)
                    """);
            await migrator.MigrateAsync();
            db.ChangeTracker.Clear();
            var upgraded = await db.RefreshTokens.AsNoTracking().ToListAsync();
            Assert.Equal(2, upgraded.Count);
            Assert.All(upgraded, session =>
            {
                Assert.NotNull(session.RevokedDate);
                Assert.Null(session.ReplacedByToken);
            });
            Assert.DoesNotContain(upgraded, session => session.Token == jwt.Token || session.Token == cookie.Token);
            Assert.True(await db.Users.AnyAsync(user => user.Id == adminId));
            Assert.Equal(auditIds.OrderBy(id => id), (await db.AuditLogs.Select(log => log.Id).ToArrayAsync()).OrderBy(id => id));
        }
        Authorize(legacyJwt);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await BrowserState()).GetProperty("user").ValueKind);
        var fresh = await Login();
        Authorize(fresh);
        using var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = legacyJwt.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, oldRefresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/stats")).StatusCode);
        using var final = factory.Services.CreateScope();
        Assert.Null((await final.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.SingleAsync(session => session.Id == SessionId(fresh))).RevokedDate);
    }

    [Fact]
    public async Task PasswordResetBeforeTheLoginLockRejectsPreviouslyVerifiedCredentialsWithoutAnOrphanSession()
    {
        Authorize(await Login());
        var targetId = await CreateSessionSecurityUserAsync("before-lock@example.test");
        var barrier = new SessionLockBarrier(targetId, pauseAfterLock: false);
        await using var pausedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => DecorateSessionState(services, barrier)));
        using var pausedClient = pausedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var login = pausedClient.PostAsJsonAsync("/api/auth/login", new { email = "before-lock@example.test", password = Password });
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var reset = await client.PostAsJsonAsync("/api/usermanagement/change-password", new
            { userId = targetId, newPassword = "Before-lock-new-password-593!" });
            reset.EnsureSuccessStatusCode();
        }
        finally
        {
            barrier.Release.TrySetResult(true);
        }
        using var response = await login.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var check = factory.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.RefreshTokens.AnyAsync(session => session.UserId == targetId));
        Assert.False(await db.AuditLogs.AnyAsync(log => log.UserId == targetId.ToString() && log.Action == "Login"));
        using var fresh = await client.PostAsJsonAsync("/api/auth/login", new
        { email = "before-lock@example.test", password = "Before-lock-new-password-593!" });
        fresh.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OppositeBrowserAccountSwapsAcquireUserLocksWithoutDeadlocking()
    {
        var otherId = await CreateSessionSecurityUserAsync("browser-swap@example.test");
        using var firstBrowser = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var secondBrowser = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await SignInSessionBrowserAsync(firstBrowser, "admin@example.test");
        await SignInSessionBrowserAsync(secondBrowser, "browser-swap@example.test");
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
        await using var blocker = new NpgsqlConnection(connectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using var rowLock = new NpgsqlCommand("""SELECT "Id" FROM "AspNetUsers" WHERE "Id" = @id FOR UPDATE""", blocker, transaction);
        rowLock.Parameters.AddWithValue("id", new[] { adminId, otherId }.OrderBy(id => id).First());
        await rowLock.ExecuteScalarAsync();
        var firstSwap = firstBrowser.PostAsJsonAsync("/api/admin-session/login", new { email = "browser-swap@example.test", password = Password });
        var secondSwap = secondBrowser.PostAsJsonAsync("/api/admin-session/login", new { email = "admin@example.test", password = Password });
        try
        {
            await WaitForBlockedSessionRequestsAsync([firstSwap, secondSwap], minimumWaiters: 2);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
        using var first = await firstSwap.WaitAsync(TimeSpan.FromSeconds(30));
        using var second = await secondSwap.WaitAsync(TimeSpan.FromSeconds(30));
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var firstState = await firstBrowser.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin-session");
        var secondState = await secondBrowser.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin-session");
        Assert.Equal(otherId.ToString(), firstState.GetProperty("data").GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(adminId.ToString(), secondState.GetProperty("data").GetProperty("user").GetProperty("id").GetString());
        using var check = factory.Services.CreateScope();
        Assert.Equal(2, await check.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.CountAsync(session => session.RevokedDate == null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentRefreshCannotEscapeAccountWideInvalidation(bool resetPassword)
    {
        Authorize(await Login());
        var targetId = await CreateSessionSecurityUserAsync("refresh-race@example.test");
        using var originalLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = "refresh-race@example.test", password = Password });
        originalLogin.EnsureSuccessStatusCode();
        var original = (await originalLogin.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        var barrier = new SessionLockBarrier(targetId, pauseAfterLock: true);
        await using var pausedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => DecorateSessionState(services, barrier)));
        using var pausedClient = pausedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var pendingRefresh = pausedClient.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = original.RefreshToken });
        Task<HttpResponseMessage>? administration = null;
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            administration = resetPassword
                ? client.PostAsJsonAsync("/api/usermanagement/change-password", new { userId = targetId, newPassword = "Refresh-race-new-password-593!" })
                : client.PostAsJsonAsync("/api/usermanagement/revoke-all", new { userId = targetId });
            await WaitForCompletionOrDatabaseLockAsync(administration, barrier.BlockingBackendPid);
        }
        finally
        {
            barrier.Release.TrySetResult(true);
        }
        using var rotated = await pendingRefresh.WaitAsync(TimeSpan.FromSeconds(30));
        using var invalidated = await administration!.WaitAsync(TimeSpan.FromSeconds(30));
        rotated.EnsureSuccessStatusCode();
        var token = (await rotated.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        using var targetClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        if (invalidated.StatusCode == HttpStatusCode.Conflict)
        {
            // A Serializable caller can lose its old snapshot. A 409 is a failed operation, followed by an explicit caller retry.
            Assert.False((await invalidated.Content.ReadFromJsonAsync<ServiceResponse<string>>())!.Succeeded);
            Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
            using var retry = resetPassword
                ? await client.PostAsJsonAsync("/api/usermanagement/change-password", new { userId = targetId, newPassword = "Refresh-race-new-password-593!" })
                : await client.PostAsJsonAsync("/api/usermanagement/revoke-all", new { userId = targetId });
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, invalidated.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        using var staleRefresh = await targetClient.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = token.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, staleRefresh.StatusCode);
    }

    private async Task<Guid> CreateSessionSecurityUserAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = email, Email = email, FirstName = "Session", LastName = "Test", IsActive = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "SuperAdmin")).Succeeded);
        return user.Id;
    }

    private static async Task SignInSessionBrowserAsync(HttpClient browser, string email)
    {
        var state = await browser.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/admin-session");
        browser.DefaultRequestHeaders.Add("X-CSRF-Token", state.GetProperty("data").GetProperty("csrfToken").GetString());
        using var login = await browser.PostAsJsonAsync("/api/admin-session/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        var authenticated = (await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("data");
        browser.DefaultRequestHeaders.Remove("X-CSRF-Token");
        browser.DefaultRequestHeaders.Add("X-CSRF-Token", authenticated.GetProperty("csrfToken").GetString());
    }

    private async Task WaitForBlockedSessionRequestsAsync(Task[] operations, int minimumWaiters)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(timeout.Token);
        await using var query = new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type = 'Lock'
            """, observer);
        while (operations.All(operation => !operation.IsCompleted))
        {
            if ((long)(await query.ExecuteScalarAsync(timeout.Token))! >= minimumWaiters) return;
            await Task.WhenAny(Task.WhenAny(operations), Task.Delay(25, timeout.Token));
            timeout.Token.ThrowIfCancellationRequested();
        }
        Assert.Fail("A browser swap completed before both requests were observed waiting on the held user row.");
    }

    private static void DecorateSessionState(IServiceCollection services, SessionLockBarrier barrier)
    {
        var registration = services.Last(service => service.ServiceType == typeof(IUserSessionState));
        services.Remove(registration);
        services.AddScoped<IUserSessionState>(provider =>
        {
            var inner = (IUserSessionState)(registration.ImplementationInstance
                ?? registration.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!));
            return new PausedUserSessionState(inner, provider.GetRequiredService<AppDbContext>(), barrier);
        });
    }

    private sealed class SessionLockBarrier(Guid targetUserId, bool pauseAfterLock)
    {
        private int entered;
        public Guid TargetUserId { get; } = targetUserId;
        public bool PauseAfterLock { get; } = pauseAfterLock;
        public int BlockingBackendPid { get; set; }
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryEnter() => Interlocked.CompareExchange(ref entered, 1, 0) == 0;
    }

    private sealed class PausedUserSessionState(IUserSessionState inner, AppDbContext db, SessionLockBarrier barrier) : IUserSessionState
    {
        public async Task<IUserSessionTransaction> LockAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken)
        {
            var ids = userIds.ToArray();
            var pause = ids.Contains(barrier.TargetUserId) && barrier.TryEnter();
            if (pause && !barrier.PauseAfterLock) await PauseAsync(cancellationToken);
            var transaction = await inner.LockAsync(ids, cancellationToken);
            try
            {
                if (pause && barrier.PauseAfterLock) await PauseAsync(cancellationToken);
                return transaction;
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        public Task<IReadOnlyList<Guid>> RevokeAllAsync(AppUser user, string reason, string? ipAddress,
            CancellationToken cancellationToken, bool rotateSecurityStamp = true)
            => inner.RevokeAllAsync(user, reason, ipAddress, cancellationToken, rotateSecurityStamp);

        private async Task PauseAsync(CancellationToken cancellationToken)
        {
            if (db.Database.GetDbConnection() is NpgsqlConnection { State: ConnectionState.Open } connection)
                barrier.BlockingBackendPid = connection.ProcessID;
            barrier.Reached.TrySetResult(true);
            await barrier.Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private async Task AssertConcurrentLoginIsInvalidatedAsync(bool resetPassword)
    {
        Authorize(await Login());
        Guid targetId;
        const string targetEmail = "session-race@example.test";
        const string newPassword = "Session-race-new-password-593!";
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = new AppUser { UserName = targetEmail, Email = targetEmail, FirstName = "Session", LastName = "Race", IsActive = true };
            Assert.True((await users.CreateAsync(target, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(target, "SuperAdmin")).Succeeded);
            targetId = target.Id;
        }

        var barrier = new LoginAuditBarrier(targetId);
        await using var pausedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddScoped<AuditService>();
            services.AddScoped<IAuditService>(provider => new PausedLoginAuditService(
                provider.GetRequiredService<AuditService>(), provider.GetRequiredService<AppDbContext>(), barrier));
        }));
        using var pausedClient = pausedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var pendingLogin = pausedClient.PostAsJsonAsync("/api/auth/login", new { email = targetEmail, password = Password });
        Task<HttpResponseMessage>? administration = null;
        try
        {
            // Pause after password verification/token staging, before the login's session/audit save.
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            administration = resetPassword
                ? client.PostAsJsonAsync("/api/usermanagement/change-password", new { userId = targetId, newPassword })
                : client.PostAsJsonAsync("/api/usermanagement/revoke-all", new { userId = targetId });
            // Old code completes the management operation first; a serialized implementation waits on the login's row lock.
            await WaitForCompletionOrDatabaseLockAsync(administration, barrier.BlockingBackendPid);
        }
        finally
        {
            barrier.Release.TrySetResult(true);
        }

        using var loginResponse = await pendingLogin.WaitAsync(TimeSpan.FromSeconds(30));
        using var administrationResponse = await administration!.WaitAsync(TimeSpan.FromSeconds(30));
        loginResponse.EnsureSuccessStatusCode();
        administrationResponse.EnsureSuccessStatusCode();
        var oldToken = (await loginResponse.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        using var targetClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        targetClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await targetClient.GetAsync("/api/dashboard/stats")).StatusCode);
        using var refresh = await targetClient.PostAsJsonAsync("/api/auth/refresh-token", new { refreshToken = oldToken.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        if (resetPassword)
        {
            using var oldPasswordLogin = await targetClient.PostAsJsonAsync("/api/auth/login", new { email = targetEmail, password = Password });
            Assert.Equal(HttpStatusCode.BadRequest, oldPasswordLogin.StatusCode);
        }
        using var freshLogin = await targetClient.PostAsJsonAsync("/api/auth/login", new
        { email = targetEmail, password = resetPassword ? newPassword : Password });
        freshLogin.EnsureSuccessStatusCode();
    }

    private async Task WaitForCompletionOrDatabaseLockAsync(Task operation, int blockingBackendPid)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var connectionString = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(timeout.Token);
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM pg_stat_activity
                WHERE datname = current_database() AND pid <> pg_backend_pid()
                    AND wait_event_type = 'Lock'
                    AND (@blocker = 0 OR @blocker = ANY(pg_blocking_pids(pid)))
            )
            """, observer);
        query.Parameters.AddWithValue("blocker", blockingBackendPid);
        while (!operation.IsCompleted)
        {
            if ((bool)(await query.ExecuteScalarAsync(timeout.Token))!) return;
            await Task.WhenAny(operation, Task.Delay(25, timeout.Token));
            timeout.Token.ThrowIfCancellationRequested();
        }
    }

    private sealed class LoginAuditBarrier(Guid targetUserId)
    {
        public Guid TargetUserId { get; } = targetUserId;
        public int BlockingBackendPid { get; set; }
        public TaskCompletionSource<bool> Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PausedLoginAuditService(AuditService inner, AppDbContext db, LoginAuditBarrier barrier) : IAuditService
    {
        public async Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress)
        {
            if (action == "Login" && entityId == barrier.TargetUserId.ToString())
            {
                if (db.Database.GetDbConnection() is NpgsqlConnection { State: ConnectionState.Open } connection)
                    barrier.BlockingBackendPid = connection.ProcessID;
                barrier.Reached.TrySetResult(true);
                await barrier.Release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            await inner.LogAsync(userId, action, entityName, entityId, details, ipAddress);
        }
    }
}
