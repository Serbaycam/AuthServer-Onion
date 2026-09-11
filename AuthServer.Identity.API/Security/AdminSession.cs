using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthServer.Identity.API.Security;

public static class AdminSession
{
    public const string Scheme = "AdminPanel";
    public const string Selector = "BearerOrAdminPanel";
    public const string CookieName = "AuthServer.AdminSession";
}

public sealed class AdminSessionOptions
{
    public int LifetimeHours { get; set; } = 12;
    public bool RequireHttps { get; set; } = true;
}

// Cookie tickets are checked against the same revocable session records as JWTs.
public sealed class AdminSessionEvents(IApplicationDbContext db, UserManager<AppUser> users) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = id == null ? null : await users.FindByIdAsync(id);
        if (user == null || !user.IsActive || user.TwoFactorEnabled || await users.IsLockedOutAsync(user) ||
            principal?.FindFirstValue("security_stamp") != user.SecurityStamp ||
            !Guid.TryParse(principal?.FindFirstValue("sid"), out var sid) ||
            !await db.RefreshTokens.AsNoTracking().AnyAsync(t => t.Id == sid && t.UserId == user.Id &&
                t.RevokedDate == null && t.Expires > DateTime.UtcNow, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AdminSession.Scheme);
            return;
        }

        var roles = await users.GetRolesAsync(user);
        var identity = new ClaimsIdentity(AdminSession.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        identity.AddClaims(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim("security_stamp", user.SecurityStamp ?? ""),
            new Claim("sid", sid.ToString())
        });
        identity.AddClaims(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        context.ReplacePrincipal(new ClaimsPrincipal(identity));
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    { context.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; }
}
