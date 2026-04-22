using AuthServer.Identity.API.Middlewares;
using AuthServer.Identity.Application;
using AuthServer.Identity.Domain.Constants;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Infrastructure;
using AuthServer.Identity.Persistence;
using AuthServer.Identity.Persistence.Seeds;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using AuthServer.Identity.Persistence.Context;
var builder = WebApplication.CreateBuilder(args);

// --- 1. Service Registration (Servis Kayıtları) ---
builder.Services.AddMemoryCache();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAdminPanel",
        policy => policy.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});
// Kendi katmanlarımızı yüklüyoruz
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
// JWT Authentication Ayarları
// Bu ayar API'ye gelen "Authorization: Bearer <token>" başlığını okumasını sağlar.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(o =>
{
    o.RequireHttpsMetadata = false;
    o.SaveToken = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero, // Token süresi bittiği an hata versin (Varsayılan 5 dk tolerans vardır)

        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]))
    };
});
builder.Services.AddAuthorization(options =>
{
    // Permissions sınıfındaki stringleri bul
    var permissions = typeof(Permissions).GetNestedTypes()
        .SelectMany(c => c.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy))
        .Select(f => f.GetValue(null).ToString())
        .Where(p => p != null);

    foreach (var permission in permissions)
    {
        // ARTIK BURASI DEĞİŞTİ:
        // Eskiden: policy.RequireClaim(...) diyorduk.
        // Şimdi: policy.AddRequirements(new PermissionRequirement(...)) diyoruz.
        // Bu sayede bizim yazdığımız Handler devreye girecek.
        options.AddPolicy(permission, policy =>
            policy.AddRequirements(new PermissionRequirement(permission)));
    }
});
// API Controller desteği
builder.Services.AddControllers();

// OpenAPI (Swagger alternatifi yeni .NET özelliği)
builder.Services.AddOpenApi();

var app = builder.Build();

// --- 2. HTTP Request Pipeline (Middleware Sırası ÇOK ÖNEMLİDİR) ---

if (app.Environment.IsDevelopment())
{
    // .NET 9 ile gelen standart OpenAPI sayfası
    app.MapOpenApi();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();

// !!! KRİTİK BÖLÜM !!!
// Sıralama: Önce kimlik var mı? (AuthN) -> Sonra yetkisi var mı? (AuthZ)
app.UseCors("AllowAdminPanel");
app.UseAuthentication();
app.UseMiddleware<UserStatusMiddleware>();
app.UseAuthorization();
// Controller'ları endpoint olarak haritala
app.MapControllers();
// --- SEEDING BAŞLANGICI ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();
        var dbContext = services.GetRequiredService<AppDbContext>();

        // Otomatik veritabanı göçü (migration) işlemi
        // (docker container ilk ayağa kalkarken Update-Database işlemini kendisi yapar)
        await dbContext.Database.MigrateAsync();

        // Rolleri Ekle
        await ContextSeed.SeedRolesAsync(userManager, roleManager);

        // Admini Ekle
        await ContextSeed.SeedSuperAdminAsync(userManager, roleManager);

        // Role Claim'lerini Ekle
        await ContextSeed.SeedRoleClaimsAsync(roleManager);
    }
    catch (Exception ex)
    {
        // Loglama yapabilirsin
        Console.WriteLine("Seeding sırasında hata oluştu: " + ex.Message);
    }
}
// --- SEEDING BİTİŞİ ---
app.Run();