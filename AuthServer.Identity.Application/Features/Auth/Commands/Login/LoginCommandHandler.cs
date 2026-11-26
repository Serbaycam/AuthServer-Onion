using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(UserManager<AppUser> users, SignInManager<AppUser> signIn,
    ITokenService tokens, IApplicationDbContext db, IAuditService audit, ICurrentUserService actor,
    IUserSessionState sessionState) : IRequestHandler<LoginCommand, ServiceResponse<TokenDto>>
{
    public async Task<ServiceResponse<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user == null || !user.IsActive || user.TwoFactorEnabled)
            return Denied();
        // Persist failed-password counters outside the issuance transaction.
        if (!(await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true)).Succeeded)
            return Denied();
        var verifiedStamp = user.SecurityStamp;
        await using var transaction = await sessionState.LockAsync([user.Id], cancellationToken);
        user = transaction.FindUser(user.Id);
        if (user == null || !user.IsActive || user.TwoFactorEnabled || await users.IsLockedOutAsync(user) ||
            string.IsNullOrEmpty(verifiedStamp) || user.SecurityStamp != verifiedStamp)
            return Denied();

        var now = DateTime.UtcNow;
        var sid = Guid.NewGuid();
        var deadline = tokens.GetAbsoluteSessionExpiration(now);
        var roles = await users.GetRolesAsync(user);
        var token = await tokens.CreateTokenAsync(user, roles, sid, deadline);
        db.RefreshTokens.Add(new AuthServer.Identity.Domain.Entities.RefreshToken
        {
            Id = sid, UserId = user.Id, Token = RefreshTokenHash.Compute(token.RefreshToken),
            CreatedDate = now, CreatedByIp = actor.IpAddress ?? "Unknown", Expires = token.RefreshTokenExpiration,
            SecurityStamp = verifiedStamp, FamilyId = sid, FamilyCreatedAt = now, AbsoluteExpiresAt = deadline
        });
        // AuditService flushes both staged rows; the user lock is held until this transaction commits.
        await audit.LogAsync(user.Id.ToString(), "Login", "AppUser", user.Id.ToString(),
            new { Email = user.Email, Roles = roles }, actor.IpAddress ?? "Unknown", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ServiceResponse<TokenDto>(token, "Giriş başarılı.");
    }

    private static ServiceResponse<TokenDto> Denied() => new("Email veya şifre hatalı.");
}
