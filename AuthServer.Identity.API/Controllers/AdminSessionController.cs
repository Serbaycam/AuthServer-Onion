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
    AppDbContext db, IAuditService audit, IAntiforgery antiforgery, IOptions<AdminSessionOptions> options, IUserSessionState sessionState) : ControllerBase
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
        var verifiedStamp = user.SecurityStamp;
        var userId = user.Id;
        var previous = await HttpContext.AuthenticateAsync(AdminSession.Scheme);
        var previousUserId = Guid.TryParse(previous.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedOwner)
            ? (Guid?)parsedOwner : null;
        var lockIds = previousUserId.HasValue ? new[] { userId, previousUserId.Value } : new[] { userId };
        await using var transaction = await sessionState.LockAsync(lockIds, cancellationToken);
        user = transaction.FindUser(userId);
        if (user == null || !user.IsActive || user.TwoFactorEnabled || await users.IsLockedOutAsync(user) ||
            string.IsNullOrEmpty(verifiedStamp) || user.SecurityStamp != verifiedStamp)
            return Unauthorized(new { succeeded = false, message = "E-posta veya şifre hatalı ya da hesap girişe kapalı." });
        var roles = await users.GetRolesAsync(user);
        if (!roles.Contains("SuperAdmin"))
            return StatusCode(403, new { succeeded = false, message = "Bu panele erişmek için yönetici yetkisi gerekiyor." });

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(options.Value.LifetimeHours);
        var sid = Guid.NewGuid();
        var session = new RefreshToken
        {
            Id = sid, UserId = user.Id, Token = RefreshTokenHash.Compute(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
            Expires = expires.UtcDateTime, CreatedDate = now.UtcDateTime,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
            SecurityStamp = verifiedStamp, FamilyId = sid, FamilyCreatedAt = now.UtcDateTime,
            AbsoluteExpiresAt = expires.UtcDateTime
        };
        if (previousUserId.HasValue && Guid.TryParse(previous.Principal?.FindFirstValue("sid"), out var oldId))
        {
            var old = await db.RefreshTokens.SingleOrDefaultAsync(t => t.Id == oldId && t.UserId == previousUserId.Value, cancellationToken);
            if (old is { RevokedDate: null })
            {
                old.RevokedDate = now.UtcDateTime;
                old.RevokedByIp = session.CreatedByIp;
                old.ReasonRevoked = "Replaced by browser sign-in";
            }
        }
        db.RefreshTokens.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await audit.LogAsync(user.Id.ToString(), "AdminPanelLogin", "AppUser", user.Id.ToString(),
            new { SessionId = sid }, session.CreatedByIp, cancellationToken);
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
        if (Guid.TryParse(cookie.Principal?.FindFirstValue("sid"), out var sid) &&
            Guid.TryParse(cookie.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
        {
            await using var transaction = await sessionState.LockAsync([ownerId], cancellationToken);
            var session = await db.RefreshTokens.SingleOrDefaultAsync(t => t.Id == sid && t.UserId == ownerId, cancellationToken);
            if (session is { RevokedDate: null })
            {
                session.RevokedDate = DateTime.UtcNow;
                session.ReasonRevoked = "Browser logout";
                session.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
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
