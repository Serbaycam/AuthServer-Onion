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
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
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
builder.Services.AddControllers();
builder.Services.AddOpenApi();
var app = builder.Build();
app.UseMiddleware<GlobalExceptionMiddleware>();
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
