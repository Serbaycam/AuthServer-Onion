using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// 1. JWT Authentication Ekle
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
      // 1. ÝÞTE SÝHÝRLÝ SATIR BURASI!
      // Bu özellik 'true' olduðu sürece .NET senin claimlerini deðiþtirir.
      // Bunu 'false' yapýnca claimler olduðu gibi (ham haliyle) kalýr.
      options.MapInboundClaims = false;

      options.TokenValidationParameters = new TokenValidationParameters
      {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"])),

        ClockSkew = TimeSpan.Zero,

        // Artýk Token içinde "email" yazýyorsa buraya da "email" yazýyoruz.
        // Çünkü MapInboundClaims = false dedik, .NET artýk isimleri deðiþtirmiyor.
        NameClaimType = "email",
        RoleClaimType = "role"
      };
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseHttpsRedirection();

// 2. Middleware Sýralamasý (AuthN -> AuthZ)
app.UseAuthentication(); // Kimlik Kontrolü
app.UseAuthorization();  // Yetki Kontrolü

app.MapControllers();

app.Run();