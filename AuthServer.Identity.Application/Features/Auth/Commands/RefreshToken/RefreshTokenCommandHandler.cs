using AuthServer.Identity.Application.Security;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Auth.Commands.RefreshToken
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, ServiceResponse<TokenDto>>
    {
        private readonly ITokenService _tokenService;
        private readonly IApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly ICurrentUserService _currentUserService;

        public RefreshTokenCommandHandler(ITokenService tokenService, IApplicationDbContext context, UserManager<AppUser> userManager, ICurrentUserService currentUserService)
        {
            _tokenService = tokenService;
            _context = context;
            _userManager = userManager;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<TokenDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken)) return new ServiceResponse<TokenDto>("Geçersiz token.");
            var tokenHash = RefreshTokenHash.Compute(request.RefreshToken);
            // 1. Gelen Refresh Token'ı veritabanında bul
            var incomingToken = await _context.RefreshTokens
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.Token == tokenHash, cancellationToken);

            // Token yoksa hata dön
            if (incomingToken == null)
                return new ServiceResponse<TokenDto>("Geçersiz token.");

            // 2. REUSE DETECTION (Yeniden Kullanım Tespiti - GÜVENLİK)
            // Eğer bu token daha önce kullanılmışsa (RevokedDate doluysa), 
            // demek ki birisi eski bir bileti kullanmaya çalışıyor. Bu bir saldırı olabilir!
            if (incomingToken.RevokedDate != null)
            {
                // Saldırganı engellemek için bu zincire ait (bu kullanıcının) TÜM tokenlarını iptal et.
                await RevokeDescendantRefreshTokens(incomingToken, incomingToken.User, "Attempted reuse of revoked token", cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);

                return new ServiceResponse<TokenDto>("Güvenlik ihlali: Kullanılmış token tekrar denendi. Tüm oturumlar kapatıldı.");
            }

            // 3. Standart Kontroller (Süre bitmiş mi?)
            if (incomingToken.IsExpired)
            {
                // Süresi dolmuş ama henüz revoke edilmemişse, revoke et.
                incomingToken.RevokedDate = DateTime.UtcNow;
                incomingToken.ReasonRevoked = "Expired";

                await _context.SaveChangesAsync(cancellationToken);
                return new ServiceResponse<TokenDto>("Oturum süresi dolmuş. Lütfen tekrar giriş yapın.");
            }

            // 4. Yeni Tokenları Üret (ROTATION BAŞLIYOR)
            var user = incomingToken.User;
            if (!user.IsActive || await _userManager.IsLockedOutAsync(user))
                return new ServiceResponse<TokenDto>("Oturum yenilenemedi.");
            var roles = await _userManager.GetRolesAsync(user);
            var sessionId = Guid.NewGuid();
            var newTokenDto = await _tokenService.CreateTokenAsync(user, roles, sessionId);

            // 5. ESKİ TOKEN'I İPTAL ET (ÖNEMLİ!)
            // Artık bu token kullanılamaz, yerine yenisi geçti.
            incomingToken.RevokedDate = DateTime.UtcNow;
            incomingToken.RevokedByIp = _currentUserService.IpAddress;
            incomingToken.ReasonRevoked = "Replaced by new token";
            incomingToken.ReplacedByToken = RefreshTokenHash.Compute(newTokenDto.RefreshToken); // Zinciri kuruyoruz

            // 6. YENİ TOKEN'I OLUŞTUR
            var newRefreshTokenEntity = new Domain.Entities.RefreshToken
            {
                Id = sessionId,
                Token = RefreshTokenHash.Compute(newTokenDto.RefreshToken),
                Expires = newTokenDto.RefreshTokenExpiration,
                CreatedByIp = _currentUserService.IpAddress,
                CreatedDate = DateTime.UtcNow,
                UserId = user.Id
            };

            // 7. Hepsini Kaydet

            _context.RefreshTokens.Add(newRefreshTokenEntity); // Yeniyi ekle

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request consumed/revoked this credential. Never return the losing tokens.
                return new ServiceResponse<TokenDto>("Oturum yenilenemedi. Lütfen tekrar giriş yapın.");
            }

            return new ServiceResponse<TokenDto>(newTokenDto, "Token başarıyla yenilendi.");
        }

        // Yardımcı Metod: Bir hırsızlık durumunda kullanıcının tüm soy ağacını kurutur.
        private async Task RevokeDescendantRefreshTokens(Domain.Entities.RefreshToken refreshToken, AppUser user, string reason, CancellationToken cancellationToken)
        {
            // O kullanıcının henüz revoke edilmemiş tüm tokenlarını bul
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedDate == null).ToListAsync(cancellationToken);

            foreach (var token in activeTokens)
            {
                token.RevokedDate = DateTime.UtcNow;
                token.ReasonRevoked = reason;
            }
        }
    }
}