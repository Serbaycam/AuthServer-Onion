using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Commands.KillSession
{
    public class KillSessionHandler : IRequestHandler<KillSessionCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserSessionState _sessionState;

        public KillSessionHandler(IApplicationDbContext context, IAuditService auditService,
            ICurrentUserService currentUserService, IUserSessionState sessionState)
        {
            _context = context;
            _auditService = auditService;
            _currentUserService = currentUserService;
            _sessionState = sessionState;
        }

        public async Task<ServiceResponse<bool>> Handle(KillSessionCommand request, CancellationToken cancellationToken)
        {
            var ownerId = await _context.RefreshTokens.AsNoTracking().Where(token => token.Id == request.TokenId)
                .Select(token => (Guid?)token.UserId).SingleOrDefaultAsync(cancellationToken);
            if (ownerId == null) return new ServiceResponse<bool>("Oturum bulunamadı.");
            await using var session = await _sessionState.LockAsync([ownerId.Value], cancellationToken);
            var token = await _context.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == request.TokenId, cancellationToken);

            if (token == null) return new ServiceResponse<bool>("Oturum bulunamadı.");
            var revoked = await RefreshTokenRevocation.RevokeChainAsync(_context, token,
                "Terminated by Administrator (Kill Session)", _currentUserService.IpAddress, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Audit Log
            await _auditService.LogAsync(
                _currentUserService.UserId ?? "System",
                "KillSession",
                "RefreshToken",
                token.Id.ToString(),
                new { targetUserId = token.UserId, requestedSessionId = token.Id, revokedSessionIds = revoked },
                _currentUserService.IpAddress ?? "Unknown", cancellationToken
            );
            await session.CommitAsync(cancellationToken);

            return new ServiceResponse<bool>(true, revoked.Count == 0
                ? "Bu oturum zaten sonlandırılmış." : "Oturum başarıyla sonlandırıldı.");
        }
    }
}
