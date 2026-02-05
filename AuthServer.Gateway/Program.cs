var builder = WebApplication.CreateBuilder(args);

// --- 1. CORS Ekleme (Servis Kýsmý) ---
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorApp",
        policy =>
        {
            policy.WithOrigins("https://localhost:5001") // Blazor'ýn adresi
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// --- 2. CORS Kullanma (Middleware Kýsmý) ---
// DÝKKAT: MapReverseProxy'den ÖNCE olmalý!
app.UseCors("AllowBlazorApp");

app.MapReverseProxy();

app.Run();