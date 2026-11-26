using AuthServer.Identity.Application.Wrappers;
using AuthServer.Identity.Application.Interfaces;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Commands.KillSession
{
    public class KillSessionCommand : IRequest<ServiceResponse<bool>>, IManagementCommand
    {
        public Guid TokenId { get; set; }
    }
}
