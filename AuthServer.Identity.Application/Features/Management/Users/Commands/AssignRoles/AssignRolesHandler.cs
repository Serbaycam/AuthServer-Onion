using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AssignRoles
{
    public class AssignRolesHandler : IRequestHandler<AssignRolesCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public AssignRolesHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager,
            IAuditService auditService, ICurrentUserService currentUserService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");
            // Eğer liste boşsa veya null ise, otomatik olarak "Basic" rolünü ekle
            if (request.Roles == null || !request.Roles.Any())
            {
                request.Roles = new List<string> { "Basic" };
            }
            // 1. Gönderilen rollerin sistemde var olup olmadığını kontrol et
            foreach (var role in request.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    return new ServiceResponse<bool>($"'{role}' isimli rol sistemde bulunamadı.");
                }
            }

            if (!request.Roles.Contains("SuperAdmin", StringComparer.OrdinalIgnoreCase) &&
                await AuthServer.Identity.Application.Security.AdministratorGuard.IsLastActiveAdministratorAsync(_userManager, user))
                return new ServiceResponse<bool>("Son aktif yöneticinin rolü kaldırılamaz.");

            // 2. Kullanıcının mevcut rollerini al
            var currentRoles = await _userManager.GetRolesAsync(user);

            // 3. Mevcut rolleri sil (Temiz sayfa)
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return new ServiceResponse<bool>("Mevcut roller silinirken hata oluştu.");

            // 4. Yeni rolleri ekle
            var addResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!addResult.Succeeded) return new ServiceResponse<bool>("Yeni roller atanırken hata oluştu.");

            await _auditService.LogAsync(_currentUserService.UserId ?? "System", "AssignRolesCommand", "AppUser",
                user.Id.ToString(), new
                {
                    before = new { roles = currentRoles.OrderBy(role => role, StringComparer.Ordinal).ToArray() },
                    after = new { roles = (await _userManager.GetRolesAsync(user)).OrderBy(role => role, StringComparer.Ordinal).ToArray() }
                }, _currentUserService.IpAddress ?? "Unknown", cancellationToken);

            return new ServiceResponse<bool>(true, "Roller başarıyla güncellendi.");
        }
    }
}
