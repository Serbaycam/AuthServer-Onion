using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Revoke
{
    public class RevokeTokenCommandHandler : IRequestHandler<RevokeTokenCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;

        public RevokeTokenCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResponse<bool>> Handle(RevokeTokenCommand request, CancellationToken cancellationToken)
        {
            var refreshToken = await _context.RefreshTokens
                .SingleOrDefaultAsync(t => t.Token == request.Token, cancellationToken);

            // Eğer token yoksa hata dönmeyelim, zaten amaç token'ın çalışmaması. 
            // "Idempotent" (tekrar tekrar çalıştırılabilir) olması iyidir.
            if (refreshToken == null)
            {
                return new ServiceResponse<bool>("Token bulunamadı.");
            }

            // Zaten iptal edilmişse işlem yapma
            if (!refreshToken.IsActive)
            {
                return new ServiceResponse<bool>("Token zaten geçersiz.");
            }

            // Token'ı iptal et (Revoke)
            refreshToken.RevokedDate = DateTime.UtcNow;
            refreshToken.RevokedByIp = request.IpAddress;

            _context.RefreshTokens.Update(refreshToken);
            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<bool>(true, "Token başarıyla iptal edildi.");
        }
    }
}