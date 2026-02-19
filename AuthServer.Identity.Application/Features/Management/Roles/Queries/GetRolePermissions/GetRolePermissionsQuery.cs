using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Roles.Queries.GetRolePermissions
{
    public class GetRolePermissionsQuery : IRequest<ServiceResponse<List<PermissionDto>>>
    {
        public string RoleId { get; set; }

    }
}