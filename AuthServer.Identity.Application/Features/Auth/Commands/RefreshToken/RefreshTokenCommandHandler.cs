using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(ITokenService tokens, IApplicationDbContext db,
    UserManager<AppUser> users, ICurrentUserService actor, IUserSessionState sessionState)
    : IRequestHandler<RefreshTokenCommand, ServiceResponse<TokenDto>>
{
    public async Task<ServiceResponse<TokenDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)) return Denied("Geçersiz token.");
        var hash = RefreshTokenHash.Compute(request.RefreshToken);
        var ownerId = await db.RefreshTokens.AsNoTracking().Where(session => session.Token == hash)
            .Select(session => (Guid?)session.UserId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId == null) return Denied("Geçersiz token.");
        await using var transaction = await sessionState.LockAsync([ownerId.Value], cancellationToken);
        var user = transaction.FindUser(ownerId.Value);
        var incoming = await db.RefreshTokens.SingleOrDefaultAsync(session => session.Token == hash, cancellationToken);
        if (user == null || incoming == null) return Denied("Geçersiz token.");

        // Preserve user-wide replay policy; the epoch also covers concurrent, previously invisible sessions.
        if (incoming.RevokedDate != null)
        {
            await sessionState.RevokeAllAsync(user, "Attempted reuse of revoked token", actor.IpAddress, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Denied("Güvenlik ihlali: Kullanılmış token tekrar denendi. Tüm oturumlar kapatıldı.");
        }
        if (incoming.IsExpired || incoming.FamilyId == Guid.Empty ||
            string.IsNullOrEmpty(incoming.SecurityStamp) || incoming.SecurityStamp != user.SecurityStamp)
        {
            incoming.RevokedDate = DateTime.UtcNow;
            incoming.RevokedByIp = actor.IpAddress;
            incoming.ReasonRevoked = incoming.IsExpired ? "Expired" : "Account security version changed";
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Denied("Oturum süresi veya güvenlik bilgisi değişmiş. Lütfen tekrar giriş yapın.");
        }
        if (!user.IsActive || user.TwoFactorEnabled || await users.IsLockedOutAsync(user))
            return Denied("Oturum yenilenemedi.");
        var sid = Guid.NewGuid();
        var token = await tokens.CreateTokenAsync(user, await users.GetRolesAsync(user), sid, incoming.AbsoluteExpiresAt);
        if (token.RefreshTokenExpiration <= DateTime.UtcNow || token.AccessTokenExpiration <= DateTime.UtcNow)
            return Denied("Oturum süresi dolmuş. Lütfen tekrar giriş yapın.");
        incoming.RevokedDate = DateTime.UtcNow;
        incoming.RevokedByIp = actor.IpAddress;
        incoming.ReasonRevoked = "Replaced by new token";
        incoming.ReplacedByToken = RefreshTokenHash.Compute(token.RefreshToken);
        db.RefreshTokens.Add(new AuthServer.Identity.Domain.Entities.RefreshToken
        {
            Id = sid, UserId = user.Id, Token = RefreshTokenHash.Compute(token.RefreshToken),
            Expires = token.RefreshTokenExpiration, CreatedDate = DateTime.UtcNow,
            CreatedByIp = actor.IpAddress ?? "Unknown", SecurityStamp = incoming.SecurityStamp,
            FamilyId = incoming.FamilyId, FamilyCreatedAt = incoming.FamilyCreatedAt,
            AbsoluteExpiresAt = incoming.AbsoluteExpiresAt
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Denied("Oturum yenilenemedi. Lütfen tekrar giriş yapın."); }
        await transaction.CommitAsync(cancellationToken);
        return new ServiceResponse<TokenDto>(token, "Token başarıyla yenilendi.");
    }

    private static ServiceResponse<TokenDto> Denied(string message) => new(message);
}
