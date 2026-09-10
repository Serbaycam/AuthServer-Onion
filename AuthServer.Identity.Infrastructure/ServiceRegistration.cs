using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Infrastructure.Services;
using AuthServer.Identity.Infrastructure.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthServer.Identity.Infrastructure
{
    public static class ServiceRegistration
    {
        public static void AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // AppSettings.json'daki veriyi sınıfa map ediyoruz
            services.AddOptions<JwtSettings>().Bind(configuration.GetSection("JwtSettings"))
                .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret) &&
                    System.Text.Encoding.UTF8.GetByteCount(settings.Secret) >= 32 &&
                    !string.IsNullOrWhiteSpace(settings.Issuer) && !string.IsNullOrWhiteSpace(settings.Audience),
                    "Configure JWT issuer, audience and a random secret of at least 32 bytes.")
                .Validate(settings => settings.AccessTokenExpirationMinutes > 0 && settings.AccessTokenExpirationMinutes <= 60 &&
                    settings.RefreshTokenExpirationDays > 0 && settings.RefreshTokenExpirationDays <= 90,
                    "Access token lifetime must be 1–60 minutes and refresh lifetime 1–90 days.")
                .ValidateOnStart();

            // Interface ve Implementation'ı eşleştiriyoruz
            services.AddTransient<ITokenService, TokenService>();
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
        }
    }
}