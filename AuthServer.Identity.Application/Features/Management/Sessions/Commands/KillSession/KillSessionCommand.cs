using AuthServer.Identity.Application.Wrappers;
using MediatR;
using System.Text.Json.Serialization;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Commands.KillSession
{
    public class KillSessionCommand : IRequest<ServiceResponse<bool>>
    {
        public Guid TokenId { get; set; }
    }
}
