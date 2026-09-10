using AuthServer.Identity.Application.Interfaces;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRolePermissions
{
    public class UpdateRolePermissionsHandler : IRequestHandler<UpdateRolePermissionsCommand, ServiceResponse<bool>>
    {
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public UpdateRolePermissionsHandler(RoleManager<AppRole> roleManager, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _roleManager = roleManager;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
        {
            // 1. Rolü bul
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return new ServiceResponse<bool>("Rol bulunamadı.");

            var allowed = typeof(AuthServer.Identity.Domain.Constants.Permissions).GetNestedTypes()
                .SelectMany(t => t.GetFields()).Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!).ToHashSet(StringComparer.Ordinal);
            if (request.Permissions == null || request.Permissions.Any(p => !allowed.Contains(p)))
                return new ServiceResponse<bool>("Geçersiz yetki listesi.");

            // 2. Mevcut tüm "permission" claimlerini temizle
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var permissionClaims = existingClaims.Where(c => c.Type == "permission");

            foreach (var claim in permissionClaims)
            {
                var result = await _roleManager.RemoveClaimAsync(role, claim);
                if (!result.Succeeded) return new ServiceResponse<bool>("Yetki kaldırılamadı.");
            }

            // 3. Yeni yetkileri ekle
            foreach (var permission in request.Permissions.Distinct())
            {
                var result = await _roleManager.AddClaimAsync(role, new Claim("permission", permission));
                if (!result.Succeeded) return new ServiceResponse<bool>("Yetki eklenemedi.");
            }

            await _auditService.LogAsync(
                _currentUserService.UserId ?? "System", // request üzerinden alıyoruz
                "UpdateRolePermissions",
                "AppRole",
                role.Id.ToString(),
                new { NewPermissions = request.Permissions },
                _currentUserService.IpAddress ?? "127.0.0.1" // request üzerinden alıyoruz
            );
            return new ServiceResponse<bool>(true, $"{role.Name} rolüne ait yetkiler başarıyla güncellendi.");
        }
    }
}
