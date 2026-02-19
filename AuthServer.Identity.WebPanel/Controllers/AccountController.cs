using System.Security.Claims;
using AuthServer.Identity.WebPanel.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace IdmsUi.Controllers;

[AllowAnonymous]
public sealed class AccountController : Controller
{
    private readonly AuthApiClient _auth;

    public AccountController(AuthApiClient auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {

        ViewBag.ReturnUrl = returnUrl ?? "/";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password, string? returnUrl = null)
    {
        var token = await _auth.LoginAsync(email, password);
        if (token is null)
        {
            ModelState.AddModelError("", "Giriş başarısız.");
            ViewBag.ReturnUrl = returnUrl ?? "/";
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Sid, Guid.NewGuid().ToString("N")) // refresh lock için
        };
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token.AccessToken);

            // NameIdentifier (API audit için işine de yarar)
            var nameId =
                jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;

            if (!string.IsNullOrWhiteSpace(nameId))
                claims.Add(new Claim(ClaimTypes.NameIdentifier, nameId));

            // Role claimleri (farklı issuer’larda type değişebiliyor)
            var roleValues = jwt.Claims
                .Where(c =>
                    c.Type == ClaimTypes.Role ||
                    c.Type == "role" ||
                    c.Type.EndsWith("/role"))
                .Select(c => c.Value)
                .Distinct();

            foreach (var r in roleValues)
                claims.Add(new Claim(ClaimTypes.Role, r));
        }
        catch
        {
            // JWT parse edilemezse sessiz geç (UI yine çalışır, sadece role bazlı menü/policy devre dışı kalır)
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
        };

        // Token expirations -> UTC’ye normalize et
        DateTimeOffset accessExpUtc = token.AccessTokenExpiration.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(token.AccessTokenExpiration)
            : new DateTimeOffset(DateTime.SpecifyKind(token.AccessTokenExpiration, DateTimeKind.Utc));

        DateTimeOffset refreshExpUtc = token.RefreshTokenExpiration.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(token.RefreshTokenExpiration)
            : new DateTimeOffset(DateTime.SpecifyKind(token.RefreshTokenExpiration, DateTimeKind.Utc));

        AuthTicketTokenStore.Set(props, token.AccessToken, token.RefreshToken, accessExpUtc, refreshExpUtc);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

        return LocalRedirect(returnUrl ?? "/");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // Cookie'den tokenları oku
        var auth = await HttpContext.AuthenticateAsync();
        var props = auth.Properties;

        if (props is not null)
        {
            var refreshToken = AuthTicketTokenStore.GetRefreshToken(props);
            var accessToken = AuthTicketTokenStore.GetAccessToken(props);

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                // Revoke başarısız olsa bile logout devam etmeli (idempotent + UX)
                try
                {
                    await _auth.RevokeAsync(refreshToken, bearerAccessToken: accessToken);
                }
                catch
                {
                    // logla geç (opsiyonel). Kullanıcıyı logout'tan alıkoyma.
                }
            }
        }

        // UI cookie'yi kesin temizle
        await HttpContext.SignOutAsync();

        return RedirectToAction("Login");
    }
}