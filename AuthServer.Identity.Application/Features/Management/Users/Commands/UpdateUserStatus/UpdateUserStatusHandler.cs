using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUserStatus
{
    public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IApplicationDbContext _context;
        private readonly IUserSessionState _sessionState;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public UpdateUserStatusHandler(UserManager<AppUser> userManager, IApplicationDbContext context,
            IUserSessionState sessionState, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _context = context;
            _sessionState = sessionState;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
        {
            await using var session = await _sessionState.LockAsync([request.UserId], cancellationToken);
            var user = session.FindUser(request.UserId);
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");

            if (!request.IsActive && await AuthServer.Identity.Application.Security.AdministratorGuard.IsLastActiveAdministratorAsync(_userManager, user))
                return new ServiceResponse<bool>("Son aktif yönetici pasifleştirilemez.");

            var before = new { isActive = user.IsActive };
            user.IsActive = request.IsActive;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                IReadOnlyList<Guid> revoked = [];
                if (!request.IsActive)
                    revoked = await _sessionState.RevokeAllAsync(user, "User deactivated by admin",
                        _currentUserService.IpAddress, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await _auditService.LogAsync(_currentUserService.UserId ?? "System", "UpdateUserStatusCommand",
                    "AppUser", user.Id.ToString(),
                    new { before, after = new { isActive = user.IsActive }, revokedSessionIds = revoked },
                    _currentUserService.IpAddress ?? "Unknown", cancellationToken);
                await session.CommitAsync(cancellationToken);
                return new ServiceResponse<bool>(true, $"Kullanıcı durumu {(request.IsActive ? "Aktif" : "Pasif")} olarak güncellendi.");
            }

            return new ServiceResponse<bool>("Güncelleme sırasında hata oluştu.");
        }
    }
}
