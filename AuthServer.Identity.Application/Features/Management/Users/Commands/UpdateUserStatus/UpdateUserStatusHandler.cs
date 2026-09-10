using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUserStatus
{
    public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IApplicationDbContext _context;

        public UpdateUserStatusHandler(UserManager<AppUser> userManager, IApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<ServiceResponse<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");

            if (!request.IsActive && await AuthServer.Identity.Application.Security.AdministratorGuard.IsLastActiveAdministratorAsync(_userManager, user))
                return new ServiceResponse<bool>("Son aktif yönetici pasifleştirilemez.");

            user.IsActive = request.IsActive; // Kullanıcıyı pasife çek

            // KRİTİK: Eğer kullanıcı pasife çekiliyorsa, tüm Refresh Token'larını iptal etmeliyiz.
            if (!request.IsActive)
            {
                var activeTokens = await _context.RefreshTokens
                    .Where(t => t.UserId == user.Id && t.RevokedDate == null)
                    .ToListAsync(cancellationToken);

                foreach (var token in activeTokens)
                {
                    token.RevokedDate = DateTime.UtcNow;
                    token.ReasonRevoked = "User deactivated by admin";
                }
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _context.SaveChangesAsync(cancellationToken);
                return new ServiceResponse<bool>(true, $"Kullanıcı durumu {(request.IsActive ? "Aktif" : "Pasif")} olarak güncellendi.");
            }

            return new ServiceResponse<bool>("Güncelleme sırasında hata oluştu.");
        }
    }
}