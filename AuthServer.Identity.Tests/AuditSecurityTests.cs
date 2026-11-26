using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Data.Common;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthServer.Identity.Tests;

public sealed partial class IdentitySecurityTests
{
    private const string AuditServerCorrelation = "server-generated-audit-correlation";
    private const string AuditPeerAddress = "192.0.2.44";
    private const string AuditNewPassword = "New-audit-password-840!";

    [Fact]
    public async Task ManagementCreateWritesExactlyOneTargetedAuditEvent()
    {
        Authorize(await Login());
        var before = await AuditIds();
        await using var tracedFactory = CreateAuditFactory();
        using var tracedClient = CreateAuditClient(tracedFactory);
        using var response = await tracedClient.PostAsJsonAsync("/api/usermanagement/create-user", new
        {
            email = "single-audit@example.test", firstName = "Single", lastName = "Audit",
            password = Password, roles = new[] { "Basic" }
        });
        response.EnsureSuccessStatusCode();
        var createdId = (await response.Content.ReadFromJsonAsync<ServiceResponse<Guid>>())!.Data;

        var entry = Assert.Single(await NewAuditEntries(before));
        using var document = AssertAuditEnvelope(entry, "CreateUserByAdmin", "AppUser", createdId.ToString());
        AssertAuditHasNoSensitiveFields(entry.Details);
        Assert.DoesNotContain(Password, entry.Details);
    }

    [Fact]
    public async Task ManagementAssignRolesAuditIncludesTargetAndBeforeAfterChanges()
    {
        var scenario = await PrepareAuditScenario("AssignRolesCommand");
        var before = await AuditIds();
        await using var tracedFactory = CreateAuditFactory();
        using var tracedClient = CreateAuditClient(tracedFactory);
        using var response = await SendAuditScenario(tracedClient, scenario);
        response.EnsureSuccessStatusCode();

        var entry = Assert.Single(await NewAuditEntries(before));
        // A generic management event with an empty target cannot explain whose privileges changed.
        Assert.Equal(scenario.EntityId, entry.EntityId);
        using var document = AssertAuditEnvelope(entry, scenario.Action, scenario.EntityName, scenario.EntityId);
        var changes = document.RootElement.GetProperty("changes");
        Assert.Equal(new[] { "Basic" }, AuditStrings(changes.GetProperty("before").GetProperty("roles")));
        Assert.Equal(new[] { "AuditOriginalRole" }, AuditStrings(changes.GetProperty("after").GetProperty("roles")));
    }

    [Theory]
    [InlineData("UpdateUserInfo")]
    [InlineData("AdminChangePassword")]
    [InlineData("UpdateUserStatusCommand")]
    [InlineData("RevokeAllTokensCommand")]
    [InlineData("CreateRoleCommand")]
    [InlineData("UpdateRoleCommand")]
    [InlineData("DeleteRoleCommand")]
    [InlineData("UpdateRolePermissions")]
    [InlineData("KillSession")]
    public async Task EveryOtherManagementMutationWritesOneStructuredAuditEvent(string action)
    {
        var scenario = await PrepareAuditScenario(action);
        var before = await AuditIds();
        await using var tracedFactory = CreateAuditFactory();
        using var tracedClient = CreateAuditClient(tracedFactory);
        using var response = await SendAuditScenario(tracedClient, scenario);
        response.EnsureSuccessStatusCode();

        var expectedId = scenario.EntityId;
        if (action == "CreateRoleCommand")
        {
            using var scope = factory.Services.CreateScope();
            expectedId = (await scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>()
                .FindByNameAsync("AuditCreatedRole"))!.Id.ToString();
        }
        var entry = Assert.Single(await NewAuditEntries(before));
        using var document = AssertAuditEnvelope(entry, action, scenario.EntityName, expectedId);
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("changes").ValueKind);
        Assert.NotEmpty(document.RootElement.GetProperty("changes").EnumerateObject());
        AssertAuditHasNoSensitiveFields(entry.Details);
    }

    [Theory]
    [InlineData("RevokeAllTokensCommand")]
    [InlineData("KillSession")]
    public async Task IdempotentManagementSuccessStillHasOneTargetedAuditEvent(string action)
    {
        var scenario = await PrepareAuditScenario(action);
        using (var first = await SendAuditScenario(client, scenario)) first.EnsureSuccessStatusCode();
        var before = await AuditIds();
        await using var tracedFactory = CreateAuditFactory();
        using var tracedClient = CreateAuditClient(tracedFactory);
        using var repeated = await SendAuditScenario(tracedClient, scenario);
        repeated.EnsureSuccessStatusCode();

        var entry = Assert.Single(await NewAuditEntries(before));
        using var document = AssertAuditEnvelope(entry, action, scenario.EntityName, scenario.EntityId);
        AssertAuditHasNoSensitiveFields(document.RootElement.GetRawText());
    }

    [Fact]
    public async Task PasswordAuditNeverContainsPasswordsHashesStampsOrSessionCredentials()
    {
        var scenario = await PrepareAuditScenario("AdminChangePassword");
        string passwordHash;
        string securityStamp;
        using (var scope = factory.Services.CreateScope())
        {
            var target = await scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>()
                .FindByIdAsync(scenario.EntityId);
            passwordHash = target!.PasswordHash!;
            securityStamp = target.SecurityStamp!;
        }
        var before = await AuditIds();
        await using var tracedFactory = CreateAuditFactory();
        using var tracedClient = CreateAuditClient(tracedFactory);
        using var response = await SendAuditScenario(tracedClient, scenario);
        response.EnsureSuccessStatusCode();

        var entry = Assert.Single(await NewAuditEntries(before));
        var serialized = JsonSerializer.Serialize(entry);
        foreach (var secret in new[] { Password, AuditNewPassword, passwordHash, securityStamp,
                     scenario.TargetToken.AccessToken, scenario.TargetToken.RefreshToken })
            Assert.DoesNotContain(secret, serialized);
        AssertAuditHasNoSensitiveFields(entry.Details);
        using var document = AssertAuditEnvelope(entry, scenario.Action, scenario.EntityName, scenario.EntityId);
        Assert.True(document.RootElement.GetProperty("changes").GetProperty("passwordChanged").GetBoolean());
    }

    [Theory]
    [InlineData("UpdateUserInfo")]
    [InlineData("AdminChangePassword")]
    [InlineData("UpdateUserStatusCommand")]
    [InlineData("CreateRoleCommand")]
    [InlineData("UpdateRoleCommand")]
    [InlineData("DeleteRoleCommand")]
    [InlineData("UpdateRolePermissions")]
    [InlineData("KillSession")]
    public async Task AuditFailureRollsBackEachRemainingManagementMutation(string action)
    {
        var scenario = await PrepareAuditScenario(action);
        var before = await AuditDatabaseSnapshot();
        await using var failingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, FailingAuditService>()));
        using var failingClient = CreateAuditClient(failingFactory);
        using var response = await SendAuditScenario(failingClient, scenario);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Simulated audit failure", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await AuditDatabaseSnapshot());
    }

    [Fact]
    public async Task AuditCancellationDoesNotCommitTheManagementMutation()
    {
        var scenario = await PrepareAuditScenario("UpdateUserStatusCommand");
        var before = await AuditDatabaseSnapshot();
        await using var cancellingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IAuditService, AuditCancellationService>()));
        using var cancellingClient = CreateAuditClient(cancellingFactory);
        using var response = await SendAuditScenario(cancellingClient, scenario);

        // The audit operation was cancelled; the HTTP request itself was not aborted.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before, await AuditDatabaseSnapshot());
    }

    [Fact]
    public async Task AuditDatabaseWriteReceivesTheRequestCancellationToken()
    {
        var scenario = await PrepareAuditScenario("UpdateUserInfo");
        using var requestCancellation = new CancellationTokenSource();
        var probe = new AuditCommandTokenProbe(requestCancellation.Token);
        await using var probingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(probe));
            services.AddSingleton<IStartupFilter>(new AuditRequestTokenFilter(requestCancellation.Token));
        }));
        using var probingClient = CreateAuditClient(probingFactory);
        using var response = await SendAuditScenario(probingClient, scenario);
        response.EnsureSuccessStatusCode();

        Assert.NotEmpty(probe.ObservedTokens);
        Assert.All(probe.ObservedTokens, observed => Assert.Equal(probe.ExpectedToken, observed));
    }

    [Fact]
    public async Task RejectedManagementRequestDoesNotWriteASuccessAudit()
    {
        Authorize(await Login());
        var before = await AuditIds();
        using var response = await client.PostAsJsonAsync("/api/usermanagement/assign-roles", new
        {
            userId = adminId, roles = new[] { "MissingAuditRole" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await NewAuditEntries(before));
    }

    private sealed record AuditScenario(string Action, string EntityName, string EntityId,
        HttpMethod Method, string Path, object? Body, TokenDto TargetToken);

    private async Task<AuditScenario> PrepareAuditScenario(string action)
    {
        Authorize(await Login());
        Guid targetId;
        Guid roleId;
        using (var scope = factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
            var role = new AppRole { Name = "AuditOriginalRole" };
            Assert.True((await roles.CreateAsync(role)).Succeeded);
            roleId = role.Id;
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var target = new AppUser
            {
                UserName = "audit-target@example.test", Email = "audit-target@example.test",
                FirstName = "Audit", LastName = "Target", IsActive = true
            };
            Assert.True((await users.CreateAsync(target, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(target, "Basic")).Succeeded);
            targetId = target.Id;
        }
        using var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "audit-target@example.test", password = Password
        });
        login.EnsureSuccessStatusCode();
        var targetToken = (await login.Content.ReadFromJsonAsync<ServiceResponse<TokenDto>>())!.Data;
        var userId = targetId.ToString();
        var roleIdText = roleId.ToString();
        var tokenId = SessionId(targetToken);

        return action switch
        {
            "AssignRolesCommand" => new(action, "AppUser", userId, HttpMethod.Post,
                "/api/usermanagement/assign-roles", new { userId = targetId, roles = new[] { "AuditOriginalRole" } }, targetToken),
            "UpdateUserInfo" => new(action, "AppUser", userId, HttpMethod.Put,
                "/api/usermanagement/update-user", new { userId = targetId, email = "changed-audit@example.test", firstName = "Changed", lastName = "Target" }, targetToken),
            "AdminChangePassword" => new(action, "AppUser", userId, HttpMethod.Post,
                "/api/usermanagement/change-password", new { userId = targetId, newPassword = AuditNewPassword }, targetToken),
            "UpdateUserStatusCommand" => new(action, "AppUser", userId, HttpMethod.Post,
                "/api/usermanagement/update-status", new { userId = targetId, isActive = false }, targetToken),
            "RevokeAllTokensCommand" => new(action, "AppUser", userId, HttpMethod.Post,
                "/api/usermanagement/revoke-all", new { userId = targetId }, targetToken),
            "CreateRoleCommand" => new(action, "AppRole", "", HttpMethod.Post,
                "/api/rolemanagement/role", new { roleName = "AuditCreatedRole" }, targetToken),
            "UpdateRoleCommand" => new(action, "AppRole", roleIdText, HttpMethod.Put,
                "/api/rolemanagement/role", new { roleId = roleIdText, newRoleName = "AuditRenamedRole" }, targetToken),
            "DeleteRoleCommand" => new(action, "AppRole", roleIdText, HttpMethod.Delete,
                "/api/rolemanagement/role/" + roleIdText, null, targetToken),
            "UpdateRolePermissions" => new(action, "AppRole", roleIdText, HttpMethod.Post,
                "/api/rolemanagement/permissions", new { roleId = roleIdText, permissions = new[] { "Permissions.Laboratories.View" } }, targetToken),
            "KillSession" => new(action, "RefreshToken", tokenId.ToString(), HttpMethod.Post,
                "/api/sessionmanagement/kill-session", new { tokenId }, targetToken),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown audit scenario.")
        };
    }

    private WebApplicationFactory<Program> CreateAuditFactory() => factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, AuditTraceFilter>()));

    private HttpClient CreateAuditClient(WebApplicationFactory<Program> selectedFactory)
    {
        var selectedClient = selectedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        selectedClient.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        selectedClient.DefaultRequestHeaders.Add("X-Correlation-ID", "untrusted-client-correlation");
        return selectedClient;
    }

    private static async Task<HttpResponseMessage> SendAuditScenario(HttpClient selectedClient, AuditScenario scenario)
    {
        using var request = new HttpRequestMessage(scenario.Method, scenario.Path);
        if (scenario.Body != null) request.Content = JsonContent.Create(scenario.Body);
        return await selectedClient.SendAsync(request);
    }

    private async Task<Guid[]> AuditIds()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.Select(entry => entry.Id).ToArrayAsync();
    }

    private async Task<AuditLog[]> NewAuditEntries(Guid[] before)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking().Where(entry => !before.Contains(entry.Id)).ToArrayAsync();
    }

    private JsonDocument AssertAuditEnvelope(AuditLog entry, string action, string entityName, string entityId)
    {
        Assert.Equal(adminId.ToString(), entry.UserId);
        Assert.Equal(action, entry.Action);
        Assert.Equal(entityName, entry.EntityName);
        Assert.Equal(entityId, entry.EntityId);
        Assert.Equal(AuditPeerAddress, entry.IpAddress);
        var document = JsonDocument.Parse(entry.Details);
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(AuditServerCorrelation, document.RootElement.GetProperty("correlationId").GetString());
        Assert.Equal("Succeeded", document.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("changes").ValueKind);
        return document;
    }

    private static string[] AuditStrings(JsonElement value) => value.EnumerateArray()
        .Select(item => item.GetString()!).OrderBy(item => item, StringComparer.Ordinal).ToArray();

    private static void AssertAuditHasNoSensitiveFields(string details)
    {
        var prohibitedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password", "newPassword", "confirmPassword", "passwordHash", "securityStamp",
            "token", "accessToken", "refreshToken", "resetToken", "secret", "authorization", "cookie"
        };
        using var document = JsonDocument.Parse(details);
        void Inspect(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                {
                    Assert.False(prohibitedNames.Contains(property.Name), $"Sensitive audit property: {property.Name}");
                    Inspect(property.Value);
                }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Inspect(item);
        }
        Inspect(document.RootElement);
    }

    private async Task<string> AuditDatabaseSnapshot()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        static object Values(EntityEntry entry) => entry.CurrentValues.Properties.OrderBy(property => property.Name)
            .ToDictionary(property => property.Name, property => entry.Property(property.Name).CurrentValue);
        return JsonSerializer.Serialize(new
        {
            users = (await db.Users.OrderBy(user => user.Id).ToListAsync()).Select(user => Values(db.Entry(user))).ToArray(),
            roles = (await db.Roles.OrderBy(role => role.Id).ToListAsync()).Select(role => Values(db.Entry(role))).ToArray(),
            memberships = (await db.UserRoles.OrderBy(item => item.UserId).ThenBy(item => item.RoleId).ToListAsync())
                .Select(item => Values(db.Entry(item))).ToArray(),
            roleClaims = (await db.RoleClaims.OrderBy(item => item.Id).ToListAsync()).Select(item => Values(db.Entry(item))).ToArray(),
            sessions = (await db.RefreshTokens.OrderBy(item => item.Id).ToListAsync()).Select(item => Values(db.Entry(item))).ToArray(),
            audits = (await db.AuditLogs.OrderBy(item => item.Id).ToListAsync()).Select(item => Values(db.Entry(item))).ToArray()
        });
    }

    private sealed class AuditTraceFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, runNext) =>
            {
                context.TraceIdentifier = AuditServerCorrelation;
                context.Connection.RemoteIpAddress = IPAddress.Parse(AuditPeerAddress);
                await runNext(context);
            });
            next(app);
        };
    }

    private sealed class AuditCancellationService : IAuditService
    {
        public Task LogAsync(string userId, string action, string entityName, string entityId, object details, string ipAddress)
            => throw new OperationCanceledException(new CancellationToken(canceled: true));
    }

    private sealed class AuditRequestTokenFilter(CancellationToken requestToken) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, runNext) =>
            {
                context.RequestAborted = requestToken;
                await runNext(context);
            });
            next(app);
        };
    }

    private sealed class AuditCommandTokenProbe(CancellationToken expectedToken) : DbCommandInterceptor
    {
        public CancellationToken ExpectedToken { get; } = expectedToken;
        public List<CancellationToken> ObservedTokens { get; } = [];

        private void Observe(DbCommand command, CancellationToken cancellationToken)
        {
            if (command.CommandText.Contains("INSERT INTO \"AuditLogs\"", StringComparison.Ordinal))
                ObservedTokens.Add(cancellationToken);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Observe(command, cancellationToken);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Observe(command, cancellationToken);
            return ValueTask.FromResult(result);
        }
    }
}
