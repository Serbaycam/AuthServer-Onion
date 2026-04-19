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
        private readonly IMemoryCache _cache;
        private readonly IAuditService _auditService;
        private readonly ICurrentUserService _currentUserService;

        public UpdateRolePermissionsHandler(RoleManager<AppRole> roleManager, IMemoryCache cache, IAuditService auditService, ICurrentUserService currentUserService)
        {
            _roleManager = roleManager;
            _cache = cache;
            _auditService = auditService;
            _currentUserService = currentUserService;
        }

        public async Task<ServiceResponse<bool>> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
        {
            // 1. Rolü bul
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return new ServiceResponse<bool>("Rol bulunamadı.");

            // 2. Mevcut tüm "permission" claimlerini temizle
            var existingClaims = await _roleManager.GetClaimsAsync(role);
            var permissionClaims = existingClaims.Where(c => c.Type == "permission");

            foreach (var claim in permissionClaims)
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }

            // 3. Yeni yetkileri ekle
            foreach (var permission in request.Permissions)
            {
                await _roleManager.AddClaimAsync(role, new Claim("permission", permission));
            }

            // --- KRİTİK ADIM: CACHE INVALIDATION ---
            // Yetkiler değiştiği için tüm yetki cache'ini temizlemeliyiz. 
            // Basitlik adına tüm cache'i veya ilgili kullanıcıların cache'ini silebilirsin.
            // Şimdilik sistem genelinde bir "yetki değişikliği" olduğunu işaretlemek için cache'i temizliyoruz.
            if (_cache is MemoryCache memoryCache)
            {
                memoryCache.Compact(1.0); // Tüm memory cache'i temizle (Agresif ama kesin çözüm)
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
