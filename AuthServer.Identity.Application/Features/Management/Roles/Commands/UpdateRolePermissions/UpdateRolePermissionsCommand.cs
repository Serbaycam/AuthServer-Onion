using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRolePermissions
{
    public class UpdateRolePermissionsCommand : IRequest<ServiceResponse<bool>>
    {
        public string RoleId { get; set; }
        public List<string> Permissions { get; set; } // Örn: ["Permissions.Laboratories.View", "Permissions.Laboratories.Create"]
    }
}
