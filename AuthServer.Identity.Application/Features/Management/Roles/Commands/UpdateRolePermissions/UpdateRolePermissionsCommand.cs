using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRolePermissions
{
    public class UpdateRolePermissionsCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string RoleId { get; set; }
        public List<string> Permissions { get; set; } // Örn: ["Permissions.Laboratories.View", "Permissions.Laboratories.Create"]
    }
}
