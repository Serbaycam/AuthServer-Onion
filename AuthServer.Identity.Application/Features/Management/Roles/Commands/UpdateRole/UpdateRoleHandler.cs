using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRole
{
    public class UpdateRoleHandler : IRequestHandler<UpdateRoleCommand, ServiceResponse<bool>>
    {
        private readonly RoleManager<AppRole> _roleManager;
        public UpdateRoleHandler(RoleManager<AppRole> roleManager) => _roleManager = roleManager;

        public async Task<ServiceResponse<bool>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return new ServiceResponse<bool>("Rol bulunamadı.");

            if (role.Name is "SuperAdmin" or "Basic") return new ServiceResponse<bool>("Sistem rolünün adı değiştirilemez.");
            role.Name = request.NewRoleName;
            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded) return new ServiceResponse<bool>("Rol güncellenemedi.") { Errors = result.Errors.Select(e => e.Description).ToList() };

            return new ServiceResponse<bool>(true, "Rol güncellendi.");
        }
    }
}
