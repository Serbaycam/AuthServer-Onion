using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Users.Commands.UpdateUserStatus
{
    public class UpdateUserStatusCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        public Guid UserId { get; set; }
        public bool IsActive { get; set; }
    }
}
