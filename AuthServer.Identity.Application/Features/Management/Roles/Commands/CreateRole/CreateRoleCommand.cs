using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Roles.Commands.CreateRole
{
    public class CreateRoleCommand : IRequest<ServiceResponse<string>>, IManagementCommand
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string RoleName { get; set; }
    }
}
