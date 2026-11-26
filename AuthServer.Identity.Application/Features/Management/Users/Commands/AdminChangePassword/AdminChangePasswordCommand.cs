using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.AdminChangePassword
{
    public class AdminChangePasswordCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        public Guid UserId { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(256)]
        public string NewPassword { get; set; }
    }
}
