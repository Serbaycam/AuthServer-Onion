using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRoles
{
    public class GetRolesHandler : IRequestHandler<GetRolesQuery, ServiceResponse<List<RoleDto>>>
    {
        private readonly RoleManager<AppRole> _roleManager;

        public GetRolesHandler(RoleManager<AppRole> roleManager) => _roleManager = roleManager;

        public async Task<ServiceResponse<List<RoleDto>>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles
                .Select(x => new RoleDto { Id = x.Id.ToString(), Name = x.Name })
                .ToListAsync(cancellationToken);

            return new ServiceResponse<List<RoleDto>>(roles);
        }
    }
}
