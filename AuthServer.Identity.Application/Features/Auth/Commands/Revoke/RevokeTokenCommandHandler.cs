using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Revoke
{
    public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public RevokeTokenCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
        {
            var tokenHash = RefreshTokenHash.Compute(request.Token);
            var refreshToken = await _context.RefreshTokens
                .SingleOrDefaultAsync(t => t.Token == tokenHash, cancellationToken);

            // Eğer token yoksa hata dönmeyelim, zaten amaç token'ın çalışmaması. 
            // "Idempotent" (tekrar tekrar çalıştırılabilir) olması iyidir.
            if (refreshToken == null)
            {
                return new ServiceResponse<bool>(true, "Oturum kapatıldı.");
            }

            var revoked = await RefreshTokenRevocation.RevokeChainAsync(_context, refreshToken,
                "Client logout", _currentUserService.IpAddress, cancellationToken);
            if (revoked.Count == 0) return new ServiceResponse<bool>(true, "Oturum kapatıldı.");

            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<bool>(true, "Token başarıyla iptal edildi.");
        }
    }
}
