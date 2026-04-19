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
            // 1. Gelen Refresh Token'ı veritabanında bul
            var incomingToken = await _context.RefreshTokens
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

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
                _context.RefreshTokens.Update(incomingToken);
                await _context.SaveChangesAsync(cancellationToken);

                return new ServiceResponse<TokenDto>("Güvenlik ihlali: Kullanılmış token tekrar denendi. Tüm oturumlar kapatıldı.");
            }

            // 3. Standart Kontroller (Süre bitmiş mi?)
            if (incomingToken.IsExpired)
            {
                // Süresi dolmuş ama henüz revoke edilmemişse, revoke et.
                incomingToken.RevokedDate = DateTime.UtcNow;
                incomingToken.ReasonRevoked = "Expired";
                _context.RefreshTokens.Update(incomingToken);
                await _context.SaveChangesAsync(cancellationToken);
                return new ServiceResponse<TokenDto>("Oturum süresi dolmuş. Lütfen tekrar giriş yapın.");
            }

            // 4. Yeni Tokenları Üret (ROTATION BAŞLIYOR)
            var user = incomingToken.User;
            var roles = await _userManager.GetRolesAsync(user);
            var newTokenDto = await _tokenService.CreateTokenAsync(user, roles);

            // 5. ESKİ TOKEN'I İPTAL ET (ÖNEMLİ!)
            // Artık bu token kullanılamaz, yerine yenisi geçti.
            incomingToken.RevokedDate = DateTime.UtcNow;
            incomingToken.RevokedByIp = _currentUserService.IpAddress;
            incomingToken.ReasonRevoked = "Replaced by new token";
            incomingToken.ReplacedByToken = newTokenDto.RefreshToken; // Zinciri kuruyoruz

            // 6. YENİ TOKEN'I OLUŞTUR
            var newRefreshTokenEntity = new Domain.Entities.RefreshToken
            {
                Token = newTokenDto.RefreshToken,
                Expires = newTokenDto.RefreshTokenExpiration,
                CreatedByIp = _currentUserService.IpAddress,
                CreatedDate = DateTime.UtcNow,
                UserId = user.Id
            };

            // 7. Hepsini Kaydet
            _context.RefreshTokens.Update(incomingToken); // Eskiyi güncelle
            _context.RefreshTokens.Add(newRefreshTokenEntity); // Yeniyi ekle

            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<TokenDto>(newTokenDto, "Token başarıyla yenilendi.");
        }

        // Yardımcı Metod: Bir hırsızlık durumunda kullanıcının tüm soy ağacını kurutur.
        private async Task RevokeDescendantRefreshTokens(Domain.Entities.RefreshToken refreshToken, AppUser user, string reason, CancellationToken cancellationToken)
        {
            // O kullanıcının henüz revoke edilmemiş tüm tokenlarını bul
            var activeTokens = _context.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedDate == null);

            foreach (var token in activeTokens)
            {
                token.RevokedDate = DateTime.UtcNow;
                token.ReasonRevoked = reason;
            }
        }
    }
}