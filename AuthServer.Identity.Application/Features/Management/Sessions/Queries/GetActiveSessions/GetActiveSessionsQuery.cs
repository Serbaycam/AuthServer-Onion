using AuthServer.Identity.Application.Dtos;
using AuthServer.Identity.Application.Wrappers;
using MediatR;

namespace AuthServer.Identity.Application.Features.Management.Sessions.Queries.GetActiveSessions
{
    public class GetActiveSessionsQuery : IRequest<ServiceResponse<List<ActiveSessionDto>>>
    {
        public Guid? CurrentSessionId { get; set; }
    }
}
