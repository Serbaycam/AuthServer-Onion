using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Infrastructure.Services;
using AuthServer.Identity.Infrastructure.Settings;

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
    }
  }
}