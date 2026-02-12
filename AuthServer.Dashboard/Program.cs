using AuthServer.Dashboard.Components;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// 1. Blazor Servisleri
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 2. Authentication (Cookie) Servisleri
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthServer.Dashboard.Cookie";
        options.LoginPath = "/login"; // Giriþ yapmamýþsa buraya at
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

// 3. Authorization ve CascadingState
builder.Services.AddCascadingAuthenticationState();

// 4. API ile konuþacak HttpClient
// DÝKKAT: Buraya API'nin çalýþtýðý portu yaz (LaunchSettings.json'dan bakabilirsin)
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7023/"); // API URL'si
});

// 5. Controller desteði (Login POST iþlemi için þart)
builder.Services.AddControllers();

var app = builder.Build();

// Pipeline Ayarlarý
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// Authentication Middleware (Sýrasý Önemli!)
app.UseAuthentication();
app.UseAuthorization();

// Controller Route'larýný ekle
app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();