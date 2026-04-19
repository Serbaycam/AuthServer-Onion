using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Auth.Commands.Login
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, ServiceResponse<TokenDto>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly IApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public LoginCommandHandler(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ITokenService tokenService, IApplicationDbContext context, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return new ServiceResponse<TokenDto>("Kullanıcı bulunamadı.");

            // Kullanıcı Pasif ise Girişi Engelle (Bunu da ekleyelim tam olsun)
            if (!user.IsActive) return new ServiceResponse<TokenDto>("Hesabınız pasif durumdadır.");

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!signInResult.Succeeded) return new ServiceResponse<TokenDto>("Email veya şifre hatalı.");

            var roles = await _userManager.GetRolesAsync(user);

            // 1. Token Üret
            var tokenDto = await _tokenService.CreateTokenAsync(user, roles);

            // 2. Refresh Token Nesnesini Hazırla
            var refreshTokenEntity = new Domain.Entities.RefreshToken
            {
                Token = tokenDto.RefreshToken,
                Expires = tokenDto.RefreshTokenExpiration,
                CreatedByIp = _currentUserService.IpAddress,
                CreatedDate = DateTime.UtcNow,
                UserId = user.Id
            };

            // 3. Veritabanına Kaydet
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync(cancellationToken);

            // --- 4. AUDIT LOG EKLEME (BURASI YENİ) ---
            await _auditService.LogAsync(
                user.Id.ToString(),       // İşlemi yapan (Login olan kişi)
                "Login",                  // Aksiyon Adı
                "AppUser",                // Etkilenen Tablo
                user.Id.ToString(),       // Kayıt ID
                new { Email = user.Email, Roles = roles }, // Detay
                _currentUserService.IpAddress // IP Adresi
            );
            // -----------------------------------------

            return new ServiceResponse<TokenDto>(tokenDto, "Giriş başarılı.");
        }
    }
}