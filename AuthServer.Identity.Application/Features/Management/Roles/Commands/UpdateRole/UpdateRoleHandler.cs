using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

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

            role.Name = request.NewRoleName;
            await _roleManager.UpdateAsync(role);

            return new ServiceResponse<bool>(true, "Rol güncellendi.");
        }
    }
}
