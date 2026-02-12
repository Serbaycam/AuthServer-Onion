using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AdminChangePassword
{
    public class AdminChangePasswordHandler : IRequestHandler<AdminChangePasswordCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public AdminChangePasswordHandler(UserManager<AppUser> userManager, IApplicationDbContext context, IAuditService auditService)
        {
            _userManager = userManager;
            _context = context;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<bool>> Handle(AdminChangePasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");

            // 1. Şifreyi Sıfırla (Token üretip resetleme yöntemi en temizidir)
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                var response = new ServiceResponse<bool>("Şifre değiştirilemedi.");
                response.Errors = errors;
                return response;
            }

            // 2. GÜVENLİK: Şifre değiştiği için TÜM açık oturumları (Refresh Token) iptal et.
            var activeTokens = await _context.RefreshTokens
                .Where(t => t.UserId == user.Id && t.RevokedDate == null)
                .ToListAsync(cancellationToken);

            foreach (var t in activeTokens)
            {
                t.RevokedDate = DateTime.UtcNow;
                t.ReasonRevoked = "Password changed by Admin";
                t.RevokedByIp = request.IpAddress;
            }
            await _context.SaveChangesAsync(cancellationToken);

            // 3. Audit Log
            await _auditService.LogAsync(
                request.AdminId ?? "System",
                "AdminChangePassword",
                "AppUser",
                user.Id.ToString(),
                new { user.Email, Action = "Force Password Reset" },
                request.IpAddress ?? "Unknown"
            );

            return new ServiceResponse<bool>(true, "Şifre başarıyla değiştirildi ve eski oturumlar kapatıldı.");
        }
    }
}