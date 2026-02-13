using System.Security.Claims;
using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.Configure<AuthApiOptions>(builder.Configuration.GetSection("AuthApi"));
builder.Services.AddHttpClient<AuthApiClient>();

// DataProtection: dev’de bile sabitlemek iyi (cookie decrypt sorunları biter)
builder.Services.AddDataProtection()
    .SetApplicationName("AuthServer.Identity.WebPanel")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "dp_keys")));

// Server-side ticket store (cookie küçülür, tokenlar cookie’ye gömülmez)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ITicketStore, MemoryCacheTicketStore>();
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((opt, store) => opt.SessionStore = store);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "__Host-AuthServer.WebPanel";
        o.Cookie.Path = "/";
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.HttpOnly = true;

        o.LoginPath = "/account/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;

        o.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                var props = ctx.Properties;

                var accessToken = AuthTicketTokenStore.GetAccessToken(props);
                var refreshToken = AuthTicketTokenStore.GetRefreshToken(props);
                var accessExp = AuthTicketTokenStore.GetAccessExp(props);
                var refreshExp = AuthTicketTokenStore.GetRefreshExp(props);

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || accessExp is null || refreshExp is null)
                    return;

                var now = DateTimeOffset.UtcNow;

                // Refresh token bitmişse oturum düşsün
                if (refreshExp.Value <= now)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // Access token bitmeye yakın değilse çık
                if (accessExp.Value - now > TimeSpan.FromMinutes(2))
                    return;

                // lock: aynı session’da paralel refresh çakışmasın
                var sessionKey = ctx.Principal?.FindFirstValue(ClaimTypes.Sid)
                                 ?? ctx.Principal?.Identity?.Name
                                 ?? "default";

                using var _ = await SessionRefreshLock.AcquireAsync(sessionKey);

                // Lock aldıktan sonra tekrar kontrol (bir başka request yenilemiş olabilir)
                accessToken = AuthTicketTokenStore.GetAccessToken(props);
                refreshToken = AuthTicketTokenStore.GetRefreshToken(props);
                accessExp = AuthTicketTokenStore.GetAccessExp(props);

                if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || accessExp is null)
                    return;

                if (accessExp.Value - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(2))
                    return;

                var api = ctx.HttpContext.RequestServices.GetRequiredService<AuthApiClient>();
                var refreshed = await api.RefreshAsync(accessToken, refreshToken);

                if (refreshed is null)
                {
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                AuthTicketTokenStore.Set(
                    props,
                    refreshed.AccessToken,
                    refreshed.RefreshToken,
                    ToUtc(refreshed.AccessTokenExpiration),
                    ToUtc(refreshed.RefreshTokenExpiration));

                ctx.ShouldRenew = true; // cookie + sessionstore renew
            }
        };

        static DateTimeOffset ToUtc(DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Utc) return new DateTimeOffset(dt);
            if (dt.Kind == DateTimeKind.Local) return new DateTimeOffset(dt.ToUniversalTime());
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        }
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Dashboard}/{id?}");

app.Run();