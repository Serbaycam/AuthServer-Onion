using AuthServer.Identity.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace AuthServer.Identity.API.Middlewares
{
    public class UserStatusMiddleware
    {
        private readonly RequestDelegate _next;

        public UserStatusMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context, IApplicationDbContext dbContext)
        {
            // --- DÜZELTME BAŞLANGICI ---
            // Login, Register veya Refresh Token işlemi yapılıyorsa KONTROL ETME!
            // Çünkü adam zaten yeni oturum açmaya çalışıyor.
            var path = context.Request.Path.Value?.ToLower();
            if (path != null && (
                path.Contains("/api/auth/login")
                ))
            {
                await _next(context);
                return;
            }
            // --- DÜZELTME BİTİŞİ ---
            // 1. Kullanıcı giriş yapmış mı bak (Token var mı?)
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                // 2. UserId'yi al
                var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? context.User.FindFirst("uid")?.Value;

                if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out Guid userId))
                {
                    // 3. Veritabanından Kullanıcıyı ve Token Durumunu Kontrol Et
                    // En son iptal edilmemiş bir RefreshToken'ı var mı?
                    // VEYA direkt User tablosunda "SecurityStamp" kontrolü yapılabilir (daha ileri seviye).
                    // Biz şimdilik "Bu kullanıcının hiç aktif oturumu kalmış mı?" diye bakalım.

                    var hasActiveSession = await dbContext.RefreshTokens
                        .AnyAsync(t => t.UserId == userId && t.RevokedDate == null);

                    // Eğer adamın hiç aktif refresh token'ı yoksa, elindeki Access Token ile de işlem yapamazsın!
                    if (!hasActiveSession)
                    {
                        context.Response.StatusCode = 401; // Unauthorized
                        await context.Response.WriteAsync("Oturumunuz sonlandırılmıştır. (Global Logout)");
                        return; // İsteği burada kes, Controller'a gitmesin.
                    }
                }
            }

            // Sorun yoksa devam et
            await _next(context);
        }
    }
}
