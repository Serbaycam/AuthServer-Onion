using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthServer.Identity.API.Security;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Domain.Entities;
using AuthServer.Identity.Persistence.Context;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthServer.Identity.API.Controllers;

[ApiController]
[Route("api/admin-session")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminSessionController(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    AppDbContext db, IAuditService audit, IAntiforgery antiforgery, IOptions<AdminSessionOptions> options) : ControllerBase
{
    public sealed record LoginRequest(
        [Required, EmailAddress, StringLength(256)] string Email,
        [Required, StringLength(256)] string Password);

    [HttpGet]
    public async Task<IActionResult> Current()
    {
        var cookie = await HttpContext.AuthenticateAsync(AdminSession.Scheme);
        HttpContext.User = cookie.Succeeded ? cookie.Principal! : new ClaimsPrincipal(new ClaimsIdentity());
        return SessionResponse(cookie.Properties?.ExpiresUtc);
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user == null || !user.IsActive || user.TwoFactorEnabled ||
            !(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Unauthorized(new { succeeded = false, message = "E-posta veya şifre hatalı ya da hesap girişe kapalı." });
        var roles = await users.GetRolesAsync(user);
        if (!roles.Contains("SuperAdmin"))
            return StatusCode(403, new { succeeded = false, message = "Bu panele erişmek için yönetici yetkisi gerekiyor." });

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(options.Value.LifetimeHours);
        var sid = Guid.NewGuid();
        // There is no client-readable refresh secret for browser sessions.
        var session = new RefreshToken
        {
            Id = sid, UserId = user.Id, Token = RefreshTokenHash.Compute(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            Expires = expires.UtcDateTime, CreatedDate = now.UtcDateTime,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        };
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var previous = await HttpContext.AuthenticateAsync(AdminSession.Scheme);
        if (Guid.TryParse(previous.Principal?.FindFirstValue("sid"), out var oldId))
        {
            var old = await db.RefreshTokens.SingleOrDefaultAsync(t => t.Id == oldId, cancellationToken);
            if (old is { RevokedDate: null }) { old.RevokedDate = now.UtcDateTime; old.ReasonRevoked = "Replaced by browser sign-in"; }
        }
        db.RefreshTokens.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(user.Id.ToString(), "AdminPanelLogin", "AppUser", user.Id.ToString(),
            new { SessionId = sid }, session.CreatedByIp);
        await transaction.CommitAsync(cancellationToken);

        var identity = new ClaimsIdentity(AdminSession.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaims(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? ""), new Claim("sid", sid.ToString()),
            new Claim("security_stamp", user.SecurityStamp ?? "")
        });
        identity.AddClaims(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        HttpContext.User = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(AdminSession.Scheme, HttpContext.User,
            new AuthenticationProperties { IsPersistent = true, IssuedUtc = now, ExpiresUtc = expires, AllowRefresh = false });
        return SessionResponse(expires);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var cookie = await HttpContext.AuthenticateAsync(AdminSession.Scheme);
        if (Guid.TryParse(cookie.Principal?.FindFirstValue("sid"), out var sid))
        {
            var session = await db.RefreshTokens.SingleOrDefaultAsync(t => t.Id == sid, cancellationToken);
            if (session is { RevokedDate: null })
            {
                session.RevokedDate = DateTime.UtcNow;
                session.ReasonRevoked = "Browser logout";
                session.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        await HttpContext.SignOutAsync(AdminSession.Scheme);
        HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        return SessionResponse(null);
    }

    private IActionResult SessionResponse(DateTimeOffset? expires)
    {
        var principal = HttpContext.User;
        var authenticated = principal.Identity?.IsAuthenticated == true;
        return Ok(new
        {
            succeeded = true,
            data = new
            {
                user = authenticated ? new
                {
                    id = principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    email = principal.FindFirstValue(ClaimTypes.Email),
                    fullName = principal.FindFirstValue(ClaimTypes.Name),
                    roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray()
                } : null,
                expiresAt = authenticated ? expires : null,
                csrfToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken
            }
        });
    }
}
