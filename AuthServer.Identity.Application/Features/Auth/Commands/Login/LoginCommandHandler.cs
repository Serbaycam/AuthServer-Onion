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

        public LoginCommandHandler(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, ITokenService tokenService, IApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context; // Inject ettik
        }

        public async Task<ServiceResponse<TokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user == null) return new ServiceResponse<TokenDto>("Kullanıcı bulunamadı.");

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
                CreatedByIp = request.IpAddress, // IP Adresi
                CreatedDate = DateTime.UtcNow,
                UserId = user.Id
            };

            // 3. Veritabanına Kaydet
            // (Önce kullanıcının eski aktif refresh tokenlarını iptal etmek isteyebilirsin ama şimdilik çoklu oturuma izin verelim)
            _context.RefreshTokens.Add(refreshTokenEntity);
            await _context.SaveChangesAsync(cancellationToken);

            return new ServiceResponse<TokenDto>(tokenDto, "Giriş başarılı.");
        }
    }
}