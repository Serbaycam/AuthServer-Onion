using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.UpdateRole
{
    public class UpdateRoleCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string RoleId { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string NewRoleName { get; set; }
    }
}
