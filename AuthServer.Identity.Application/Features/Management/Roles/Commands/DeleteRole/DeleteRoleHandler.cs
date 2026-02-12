using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.DeleteRole
{
    public class DeleteRoleHandler : IRequestHandler<DeleteRoleCommand, ServiceResponse<bool>>
    {
        private readonly RoleManager<AppRole> _roleManager;

        public DeleteRoleHandler(RoleManager<AppRole> roleManager) => _roleManager = roleManager;

        public async Task<ServiceResponse<bool>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(request.RoleId);
            if (role == null) return new ServiceResponse<bool>("Rol bulunamadı.");

            // Temel rolleri silmeyi engelleyebilirsin (Opsiyonel Güvenlik)
            if (role.Name == "SuperAdmin" || role.Name == "Basic")
                return new ServiceResponse<bool>("Bu temel sistem rolü silinemez.");

            var result = await _roleManager.DeleteAsync(role);
            if (result.Succeeded)
                return new ServiceResponse<bool>(true, "Rol silindi.");

            return new ServiceResponse<bool>("Silme işlemi başarısız.");
        }
    }
}
