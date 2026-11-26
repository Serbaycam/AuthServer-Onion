using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AdminChangePassword
{
    public class AdminChangePasswordHandler : IRequestHandler<AdminChangePasswordCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IApplicationDbContext _context;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUserSessionState _sessionState;

        public AdminChangePasswordHandler(UserManager<AppUser> userManager, IApplicationDbContext context,
            IAuditService auditService, ICurrentUserService currentUserService, IUserSessionState sessionState)
        {
            _userManager = userManager;
            _context = context;
            _auditService = auditService;
            _currentUserService = currentUserService;
            _sessionState = sessionState;
        }

        public async Task<ServiceResponse<bool>> Handle(AdminChangePasswordCommand request, CancellationToken cancellationToken)
        {
            await using var session = await _sessionState.LockAsync([request.UserId], cancellationToken);
            var user = session.FindUser(request.UserId);
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

            // ResetPasswordAsync already rotated the stamp. Serialize revocation with session issuance.
            var revoked = await _sessionState.RevokeAllAsync(user, "Password changed by Admin",
                _currentUserService.IpAddress, cancellationToken, rotateSecurityStamp: false);
            await _context.SaveChangesAsync(cancellationToken);

            // 3. Audit Log
            await _auditService.LogAsync(
                _currentUserService.UserId ?? "System",
                "AdminChangePassword",
                "AppUser",
                user.Id.ToString(),
                new { passwordChanged = true, sessionsInvalidated = true, revokedSessionIds = revoked },
                _currentUserService.IpAddress ?? "Unknown", cancellationToken
            );
            await session.CommitAsync(cancellationToken);

            return new ServiceResponse<bool>(true, "Şifre başarıyla değiştirildi ve eski oturumlar kapatıldı.");
        }
    }
}
