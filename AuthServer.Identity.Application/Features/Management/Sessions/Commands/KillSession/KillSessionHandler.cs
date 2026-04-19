using AuthServer.Identity.Application.Interfaces;
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

        public KillSessionHandler(IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _context = context;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(KillSessionCommand request, CancellationToken cancellationToken)
        {
            var token = await _context.RefreshTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == request.TokenId, cancellationToken);

            if (token == null) return new ServiceResponse<bool>("Oturum bulunamadı.");
            if (token.RevokedDate != null) return new ServiceResponse<bool>("Bu oturum zaten sonlandırılmış.");

            // Token'ı iptal et
            token.RevokedDate = DateTime.UtcNow;
            token.RevokedByIp = _currentUserService.IpAddress;
            token.ReasonRevoked = "Terminated by Administrator (Kill Session)";

            await _context.SaveChangesAsync(cancellationToken);

            // Audit Log
            await _auditService.LogAsync(
                _currentUserService.UserId ?? "System",
                "KillSession",
                "RefreshToken",
                token.Id.ToString(),
                new { TargetUser = token.User.Email, TokenId = token.Id },
                _currentUserService.IpAddress ?? "Unknown"
            );

            return new ServiceResponse<bool>(true, "Oturum başarıyla sonlandırıldı.");
        }
    }
}
