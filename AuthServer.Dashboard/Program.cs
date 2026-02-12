using AuthServer.Dashboard.Components;
using AuthServer.Dashboard.Infrastructure.Handlers;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<AuthTokenHandler>();
// 1. Blazor Servisleri
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 2. Authentication (Cookie) Servisleri
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AuthServer.Dashboard.Cookie";
        options.LoginPath = "/login"; // Giriþ yapmamýþsa buraya at
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
    });

// 3. Authorization ve CascadingState
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddHttpClient("PublicApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7023/");
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var h = new HttpClientHandler();
    h.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
    return h;
});

// ASIL CLÝENT (Handlerlý)
// Home.razor ve diðer sayfalar bunu kullanacak
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7023/");
})
.AddHttpMessageHandler<AuthTokenHandler>() // <--- ÝÞTE AJANI BURAYA EKLEDÝK
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var h = new HttpClientHandler();
    h.ServerCertificateCustomValidationCallback = (m, c, ch, e) => true;
    return h;
});

// Blazor için (Home.razor'da @inject HttpClient Http diyorsun ya, onu handler'lý olana yönlendir)
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthApi"));

// 2. Blazor Sayfalarý (Home.razor) Ýçin
// DÝKKAT: Handler'ý "using" bloðu içine almýyoruz, her scope için new'liyoruz.
builder.Services.AddScoped(sp =>
{
    // TAZE HANDLER ÜRETÝMÝ
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

    var client = new HttpClient(handler);
    client.BaseAddress = new Uri("https://localhost:7023/"); // API adresin

    return client;
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