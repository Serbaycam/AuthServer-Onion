using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Http.Headers;

namespace AuthServer.Identity.WebPanel.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly AuthApiClient _auth;

        public DashboardController(AuthApiClient auth) => _auth = auth;

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 1. Kullanıcı hiç oturum açmamış mı?
            if (!result.Succeeded || result.Principal is null)
            {
                // Cookie yok veya geçersiz -> Login'e at
                return RedirectToAction("Login", "Account");
            }

            var properties = result.Properties;

            // 2. Token var mı? (Yeni Store yapısıyla kontrol)
            var accessToken = AuthTicketTokenStore.GetAccessToken(properties);

            if (string.IsNullOrEmpty(accessToken))
            {
                // Oturum var ama Token kaybolmuş (Çok nadir olur ama güvenlik önlemi)
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login", "Account");
            }

            // Call service (unchanged). If your service requires the token, pass `accessToken` to it.
            var data = await _auth.GetStatsAsync(accessToken);

            // Return a view (or adapt to your desired result)
            return View(data);
        }
    }
}