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
        private readonly IAuditService _auditService;

        public RefreshTokenCommandHandler(ITokenService tokenService, IApplicationDbContext context, UserManager<AppUser> userManager, IAuditService auditService)
        {
            _tokenService = tokenService;
            _context = context;
            _userManager = userManager;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<TokenDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            // 1. Veritabanındaki Refresh Token'ı bul (Kullanıcı bilgisiyle beraber - Include)
            var currentRefreshToken = await _context.RefreshTokens
                                            .Include(x => x.User)
                                            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

            // 2. Kontroller
            if (currentRefreshToken == null)
                return new ServiceResponse<TokenDto>("Token bulunamadı.");
            // Kullanıcı durumunu kontrol et
            if (!currentRefreshToken.User.IsActive)
            {
                return new ServiceResponse<TokenDto>("Hesabınız yönetici tarafından dondurulmuştur.");
            }
            if (currentRefreshToken.IsExpired)
                return new ServiceResponse<TokenDto>("Refresh token'ın süresi dolmuş. Lütfen tekrar giriş yapın.");



            // 3. Kullanıcıyı al
            var user = currentRefreshToken.User;
            if (user == null) return new ServiceResponse<TokenDto>("Kullanıcı bulunamadı.");
            if (currentRefreshToken.RevokedDate != null)
            {
                // SALDIRI TESPİT EDİLDİ: Kullanıcının tüm açık oturumlarını kapatıyoruz
                var allActiveTokens = await _context.RefreshTokens
                    .Where(x => x.UserId == user.Id && x.RevokedDate == null)
                    .ToListAsync(cancellationToken);

                foreach (var t in allActiveTokens)
                {
                    t.RevokedDate = DateTime.UtcNow;
                    t.ReasonRevoked = "Automatic Revocation due to suspected Token Theft";
                }

                await _context.SaveChangesAsync(cancellationToken);
                await _auditService.LogAsync(
                    "SYSTEM",
                    "SecurityRevocation",
                    "AppUser",
                    user.Id.ToString(),
                    new { Reason = "Suspected token theft detected during refresh" },
                    request.IpAddress ?? "Unknown"
                );
                return new ServiceResponse<TokenDto>("Güvenlik ihlali tespit edildi. Tüm oturumlar kapatıldı.");
            }
            // 4. Yeni Tokenları Üret
            var roles = await _userManager.GetRolesAsync(user);
            var newTokenDto = await _tokenService.CreateTokenAsync(user, roles);

            // 5. Token Rotation (Eskiyi iptal et, yeniyi kaydet)

            // a) Eski tokenı iptal et
            currentRefreshToken.RevokedDate = DateTime.UtcNow;
            currentRefreshToken.RevokedByIp = request.IpAddress;
            currentRefreshToken.ReplacedByToken = newTokenDto.RefreshToken;

            // b) Yeni tokenı oluştur
            var newRefreshTokenEntity = new Domain.Entities.RefreshToken
            {
                Token = newTokenDto.RefreshToken,
                Expires = newTokenDto.RefreshTokenExpiration,
                CreatedByIp = request.IpAddress,
                CreatedDate = DateTime.UtcNow,
                UserId = user.Id
            };

            // c) Veritabanına işle
            _context.RefreshTokens.Update(currentRefreshToken);
            _context.RefreshTokens.Add(newRefreshTokenEntity);

            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<TokenDto>(newTokenDto, "Token başarıyla yenilendi.");
        }
    }
}