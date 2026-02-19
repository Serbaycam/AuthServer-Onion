using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRolePermissions;
using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

public class GetRolePermissionsHandler
    : IRequestHandler<GetRolePermissionsQuery, ServiceResponse<List<PermissionDto>>>
{
    private readonly RoleManager<AppRole> _roleManager;

    public GetRolePermissionsHandler(RoleManager<AppRole> roleManager)
        => _roleManager = roleManager;

    public async Task<ServiceResponse<List<PermissionDto>>> Handle(
        GetRolePermissionsQuery request,
        CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.RoleId);
        if (role is null)
            return new ServiceResponse<List<PermissionDto>>("Rol bulunamadı.");

        var claims = await _roleManager.GetClaimsAsync(role);

        var permissions = claims
            .Where(c => c.Type == "permission")
            .Select(c => new PermissionDto { PermissionName = c.Value })
            .ToList();

        return new ServiceResponse<List<PermissionDto>>(permissions);
    }
}
