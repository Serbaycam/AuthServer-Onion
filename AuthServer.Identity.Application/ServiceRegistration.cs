using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace AuthServer.Identity.Application
{
    public static class ServiceRegistration
    {
        public static void AddApplicationServices(this IServiceCollection services)
        {
            // Bu Assembly'deki (Application projesi) tüm MediatR handler'larını bul ve ekle
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        }
    }
}