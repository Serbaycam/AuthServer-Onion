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
                ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
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
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/login");
        }
    }
}