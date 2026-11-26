using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using MediatR;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RevokeAll
{
    public class RevokeAllTokensCommandHandler : IRequestHandler<RevokeAllTokensCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserSessionState _sessionState;
        private readonly IAuditService _auditService;

        public RevokeAllTokensCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService,
            IUserSessionState sessionState, IAuditService auditService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _sessionState = sessionState;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<bool>> Handle(RevokeAllTokensCommand request, CancellationToken cancellationToken)
        {
            await using var session = await _sessionState.LockAsync([request.UserId], cancellationToken);
            var user = session.FindUser(request.UserId);
            IReadOnlyList<Guid> revoked = user == null ? [] : await _sessionState.RevokeAllAsync(user,
                "Admin force logout (Dashboard)", _currentUserService.IpAddress, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(_currentUserService.UserId ?? "System", "RevokeAllTokensCommand", "AppUser",
                request.UserId.ToString(), new { userExists = user != null, revokedSessionIds = revoked, revokedSessionCount = revoked.Count },
                _currentUserService.IpAddress ?? "Unknown", cancellationToken);
            await session.CommitAsync(cancellationToken);
            return new ServiceResponse<bool>(true, revoked.Count == 0
                ? "Aktif oturum bulunmuyor." : $"{revoked.Count} adet oturum sonlandırıldı.");
        }
    }
}
