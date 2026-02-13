using MediatR;
using Microsoft.EntityFrameworkCore;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RevokeAll
{
    public class RevokeAllTokensCommandHandler : IRequestHandler<RevokeAllTokensCommand, ServiceResponse<bool>>
    {
        private readonly IApplicationDbContext _context;

        public RevokeAllTokensCommandHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResponse<bool>> Handle(RevokeAllTokensCommand request, CancellationToken cancellationToken)
        {
            // Kullanıcının AKTİF olan tüm refresh tokenlarını bul
            var userTokens = await _context.RefreshTokens
                .Where(t => t.UserId == request.UserId && t.RevokedDate == null) // Sadece iptal edilmemişleri getir
                .ToListAsync(cancellationToken);

            if (!userTokens.Any())
            {
                return new ServiceResponse<bool>("Aktif oturum bulunamadı.");
            }

            // Hepsini iptal et
            foreach (var token in userTokens)
            {
                token.RevokedDate = DateTime.UtcNow;
                token.ReasonRevoked = "Admin force logout (Dashboard)";
                token.RevokedByIp = "Admin"; // Veya IP'yi requestten alabilirsin
            }

            _context.RefreshTokens.UpdateRange(userTokens);
            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<bool>(true, $"{userTokens.Count} adet oturum sonlandırıldı.");
        }
    }
}