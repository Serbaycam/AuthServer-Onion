using AuthServer.Identity.API.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using AuthServer.Identity.API.Middlewares;
using AuthServer.Identity.Application;
using AuthServer.Identity.Domain.Constants;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Infrastructure;
using AuthServer.Identity.Persistence;
using AuthServer.Identity.Persistence.Context;
using AuthServer.Identity.Persistence.Seeds;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Trust only explicitly configured proxy addresses/networks, and only the original scheme.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        if (!string.IsNullOrWhiteSpace(address)) options.KnownProxies.Add(System.Net.IPAddress.Parse(address));
    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
        if (!string.IsNullOrWhiteSpace(network)) options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});
builder.Services.AddMemoryCache();
builder.Services.AddCors(options => options.AddPolicy("AdminPanel", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = AdminSession.Selector;
    options.DefaultChallengeScheme = AdminSession.Selector;
    options.DefaultForbidScheme = AdminSession.Selector;
}).AddPolicyScheme(AdminSession.Selector, null, options =>
{
    options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString()
        .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? JwtBearerDefaults.AuthenticationScheme : AdminSession.Scheme;
}).AddCookie(AdminSession.Scheme, options =>
{
    options.Cookie.Name = AdminSession.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Configuration.GetValue("AdminSession:RequireHttps", true)
        ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.Path = "/api";
    options.SlidingExpiration = false;
    options.EventsType = typeof(AdminSessionEvents);
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.MapInboundClaims = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
    };
});
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var permission in typeof(Permissions).GetNestedTypes()
        .SelectMany(t => t.GetFields()).Where(f => f.IsLiteral && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!))
        options.AddPolicy(permission, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permission)));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddScoped<AdminSessionEvents>();
builder.Services.AddScoped<AdminAntiforgeryFilter>();
builder.Services.AddOptions<AdminSessionOptions>().Bind(builder.Configuration.GetSection("AdminSession"))
    .Validate(options => options.LifetimeHours is >= 1 and <= 168, "Admin session lifetime must be 1–168 hours.").ValidateOnStart();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.Name = "AuthServer.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/api";
    options.Cookie.SecurePolicy = builder.Configuration.GetValue("AdminSession:RequireHttps", true)
        ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});
var protection = builder.Services.AddDataProtection().SetApplicationName("AuthServer.AdminPanel");
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddControllers(options => options.Filters.AddService<AdminAntiforgeryFilter>());
builder.Services.AddOpenApi();
var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
else app.UseHsts();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AdminPanel");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<UserStatusMiddleware>();
app.UseAuthorization();
app.MapControllers();

// Run migrations as a deployment step in production; opt in for local/bootstrap use.
if (builder.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    await services.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    var users = services.GetRequiredService<UserManager<AppUser>>();
    var roles = services.GetRequiredService<RoleManager<AppRole>>();
    await ContextSeed.SeedRolesAsync(users, roles);
    await ContextSeed.SeedSuperAdminAsync(users, roles, builder.Configuration);
}
app.Run();

public partial class Program { }
