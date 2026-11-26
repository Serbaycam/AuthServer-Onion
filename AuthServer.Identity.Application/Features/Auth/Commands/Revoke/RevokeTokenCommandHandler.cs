using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Revoke;

public sealed class RevokeTokenCommandHandler(IApplicationDbContext db, ICurrentUserService actor,
    IUserSessionState sessionState) : IRequestHandler<RevokeTokenCommand, ServiceResponse<bool>>
{
    public async Task<ServiceResponse<bool>> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = RefreshTokenHash.Compute(request.Token);
        var ownerId = await db.RefreshTokens.AsNoTracking().Where(session => session.Token == hash)
            .Select(session => (Guid?)session.UserId).SingleOrDefaultAsync(cancellationToken);
        if (ownerId == null) return new ServiceResponse<bool>(true, "Oturum kapatıldı.");
        await using var transaction = await sessionState.LockAsync([ownerId.Value], cancellationToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(session => session.Token == hash, cancellationToken);
        if (token == null) return new ServiceResponse<bool>(true, "Oturum kapatıldı.");
        await RefreshTokenRevocation.RevokeChainAsync(db, token, "Client logout", actor.IpAddress, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ServiceResponse<bool>(true, "Oturum kapatıldı.");
    }
}
