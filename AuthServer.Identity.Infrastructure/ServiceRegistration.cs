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
            services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

            // Interface ve Implementation'ı eşleştiriyoruz
            services.AddTransient<ITokenService, TokenService>();
            services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        }
    }
}