using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuthServer.Identity.Persistence
{
    public static class ServiceRegistration
    {
        public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(ManagementTransactionBehavior<,>));
            // DbContext'i SQL Server'a bağlıyoruz
            services.AddDbContext<AppDbContext>(options =>
                {
                    var connection = configuration.GetConnectionString("DefaultConnection");
                    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.");
                    options.UseNpgsql(connection);
                });

            // --- EKLENECEK SATIR ---
            // Biri IApplicationDbContext isterse, ona yukarıda oluşturduğun AppDbContext'i ver.
            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
            // -----------------------

            // Identity Ayarları
            services.AddIdentity<AppUser, AppRole>(options =>
            {
                // Şifre kuralları (Geliştirme aşamasında gevşek bırakabilirsin)
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 12;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;

                // User Ayarları
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders(); // Şifre sıfırlama tokenları vb. için
        }
    }
}