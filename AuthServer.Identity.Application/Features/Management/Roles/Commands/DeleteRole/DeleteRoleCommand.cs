using AuthServer.Identity.Application.Wrappers;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.DeleteRole
{
    public class DeleteRoleCommand : IRequest<ServiceResponse<bool>>
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string RoleId { get; set; }
    }
}
