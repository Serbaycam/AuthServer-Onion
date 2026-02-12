using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AssignRoles
{
    public class AssignRolesHandler : IRequestHandler<AssignRolesCommand, ServiceResponse<bool>>
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;

        public AssignRolesHandler(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<ServiceResponse<bool>> Handle(AssignRolesCommand request, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null) return new ServiceResponse<bool>("Kullanıcı bulunamadı.");

            // 1. Gönderilen rollerin sistemde var olup olmadığını kontrol et
            foreach (var role in request.Roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    return new ServiceResponse<bool>($"'{role}' isimli rol sistemde bulunamadı.");
                }
            }

            // 2. Kullanıcının mevcut rollerini al
            var currentRoles = await _userManager.GetRolesAsync(user);

            // 3. Mevcut rolleri sil (Temiz sayfa)
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return new ServiceResponse<bool>("Mevcut roller silinirken hata oluştu.");

            // 4. Yeni rolleri ekle
            var addResult = await _userManager.AddToRolesAsync(user, request.Roles);
            if (!addResult.Succeeded) return new ServiceResponse<bool>("Yeni roller atanırken hata oluştu.");

            return new ServiceResponse<bool>(true, "Roller başarıyla güncellendi.");
        }
    }
}