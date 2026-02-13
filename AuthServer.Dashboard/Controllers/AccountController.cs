using AuthServer.Dashboard.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace AuthServer.Dashboard.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] LoginViewModel model)
        {
            // 1. API'ye İstek At
            var client = _httpClientFactory.CreateClient("AuthApi");
            var response = await client.PostAsJsonAsync("api/auth/login", model);

            if (!response.IsSuccessStatusCode)
            {
                // Hata varsa Login sayfasına geri dön (Query string ile hata mesajı taşıyabiliriz)
                return Redirect("/login?error=HataliGiris");
            }

            // 2. Token'ı Oku
            var responseContent = await response.Content.ReadAsStringAsync();
            // Burada API'den dönen JSON yapısına göre deserialize etmeliyiz.
            // API Response: { "data": { "accessToken": "...", ... }, "succeeded": true }
            var jsonDoc = JsonDocument.Parse(responseContent);
            var token = jsonDoc.RootElement.GetProperty("data").GetProperty("accessToken").GetString();
            var refreshToken = jsonDoc.RootElement.GetProperty("data").GetProperty("refreshToken").GetString();

            // 3. Cookie İçin Claimleri Oluştur
            // Token'ı parse edip içindeki Rolleri vs. de alabilirsin ama şimdilik basit tutalım.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, model.Email),
                new Claim("AccessToken", token), // Token'ı cookie içinde saklıyoruz!
                new Claim("RefreshToken", refreshToken)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                // DÜZELTME: Cookie ömrünü Refresh Token ömrü (örn: 7 gün) kadar yapıyoruz.
                // Böylece Access Token bitse bile Handler onu arkada yenileyebilir.
                ExpiresUtc = DateTime.UtcNow.AddDays(7)
            };

            // 4. Cookie'yi Tarayıcıya Yapıştır (HttpContext burada var!)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return Redirect("/"); // Ana sayfaya git
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            // 1. Çerezden Refresh Token'ı bulalım (Login olurken kaydetmiştik)
            var refreshToken = User.FindFirst("RefreshToken")?.Value;

            if (!string.IsNullOrEmpty(refreshToken))
            {
                try
                {
                    // 2. API'ye "Revoke" isteği atalım
                    var client = _httpClientFactory.CreateClient("AuthApi");

                    // API'deki Command yapısına uygun obje gönderiyoruz
                    // DİKKAT: RevokeTokenCommand içindeki property adı "Token" mı yoksa "RefreshToken" mı?
                    // Genelde "Token" olur. Eğer API hata verirse burayı kontrol et.
                    var command = new { Token = refreshToken };

                    // Fire and Forget yapabiliriz (cevabı çok beklemeye gerek yok)
                    // Ama beklemek daha temizdir.
                    await client.PostAsJsonAsync("api/auth/revoke-token", command);
                }
                catch
                {
                    // API kapalıysa veya hata verirse bile biz çıkış yapmalıyız.
                    // O yüzden burayı boş geçiyoruz, loglayabilirsin.
                }
            }

            // 3. Şimdi yerel oturumu (Çerezi) siliyoruz
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 4. Login sayfasına şutla
            return Redirect("/login");
        }
    }
}