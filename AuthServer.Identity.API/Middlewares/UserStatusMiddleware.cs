using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AuthServer.Identity.API.Middlewares;

public class UserStatusMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IApplicationDbContext db, UserManager<AppUser> users)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() != null ||
            context.User.Identity?.IsAuthenticated != true ||
            context.User.Identity.AuthenticationType == AuthServer.Identity.API.Security.AdminSession.Scheme)
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId == null ? null : await users.FindByIdAsync(userId);
        var now = DateTime.UtcNow;
        if (user == null || !user.IsActive || await users.IsLockedOutAsync(user) ||
            context.User.FindFirstValue("security_stamp") != user.SecurityStamp ||
            !Guid.TryParse(context.User.FindFirstValue("sid"), out var sessionId) ||
            !await db.RefreshTokens.AsNoTracking().AnyAsync(t => t.Id == sessionId &&
                t.UserId == user.Id && t.RevokedDate == null && t.Expires > now, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { succeeded = false, message = "Oturum geçersiz veya sonlandırılmış." });
            return;
        }

        // Evaluate role authorization against current membership, never stale JWT roles.
        var identity = (ClaimsIdentity)context.User.Identity!;
        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList()) identity.RemoveClaim(claim);
        foreach (var role in await users.GetRolesAsync(user)) identity.AddClaim(new Claim(identity.RoleClaimType, role));
        await next(context);
    }
}
