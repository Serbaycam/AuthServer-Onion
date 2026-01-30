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
var builder = WebApplication.CreateBuilder(args);

// --- 1. Service Registration (Servis Kayýtlarý) ---
builder.Services.AddMemoryCache();
// Kendi katmanlarýmýzý yüklüyoruz
builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApplicationServices();
// JWT Authentication Ayarlarý
// Bu ayar API'ye gelen "Authorization: Bearer <token>" baþlýðýný okumasýný saðlar.
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
        ClockSkew = TimeSpan.Zero, // Token süresi bittiði an hata versin (Varsayýlan 5 dk tolerans vardýr)

        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]))
    };
});
builder.Services.AddAuthorization(options =>
{
    // Permissions sýnýfýndaki stringleri bul
    var permissions = typeof(Permissions).GetNestedTypes()
        .SelectMany(c => c.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy))
        .Select(f => f.GetValue(null).ToString())
        .Where(p => p != null);

    foreach (var permission in permissions)
    {
        // ARTIK BURASI DEÐÝÞTÝ:
        // Eskiden: policy.RequireClaim(...) diyorduk.
        // Þimdi: policy.AddRequirements(new PermissionRequirement(...)) diyoruz.
        // Bu sayede bizim yazdýðýmýz Handler devreye girecek.
        options.AddPolicy(permission, policy =>
            policy.AddRequirements(new PermissionRequirement(permission)));
    }
});
// API Controller desteði
builder.Services.AddControllers();

// OpenAPI (Swagger alternatifi yeni .NET özelliði)
builder.Services.AddOpenApi();

var app = builder.Build();

// --- 2. HTTP Request Pipeline (Middleware Sýrasý ÇOK ÖNEMLÝDÝR) ---

if (app.Environment.IsDevelopment())
{
    // .NET 9 ile gelen standart OpenAPI sayfasý
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// !!! KRÝTÝK BÖLÜM !!!
// Sýralama: Önce kimlik var mý? (AuthN) -> Sonra yetkisi var mý? (AuthZ)
app.UseAuthentication();
app.UseAuthorization();

// Controller'larý endpoint olarak haritala
app.MapControllers();
// --- SEEDING BAÞLANGICI ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager = services.GetRequiredService<RoleManager<AppRole>>();

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
        Console.WriteLine("Seeding sýrasýnda hata oluþtu: " + ex.Message);
    }
}
// --- SEEDING BÝTÝÞÝ ---
app.Run();